namespace ItemBuckets.Support.Services
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Text;
  using System.Threading.Tasks;
  using Newtonsoft.Json;
  using Sitecore;
  using Sitecore.Buckets.Caching;
  using Sitecore.Buckets.Extensions;
  using Sitecore.Buckets.Pipelines.Search.GetFacets;
  using Sitecore.Buckets.Pipelines.UI.FetchContextData;
  using Sitecore.Buckets.Pipelines.UI.FetchContextView;
  using Sitecore.Buckets.Pipelines.UI.FillItem;
  using Sitecore.Buckets.Pipelines.UI.Search;
  using Sitecore.Buckets.Search;
  using Sitecore.Buckets.Util;
  using Sitecore.Caching;
  using Sitecore.Configuration;
  using Sitecore.ContentSearch;
  using Sitecore.ContentSearch.Diagnostics;
  using Sitecore.ContentSearch.Linq;
  using Sitecore.ContentSearch.Linq.Parsing;
  using Sitecore.ContentSearch.SearchTypes;
  using Sitecore.ContentSearch.Security;
  using Sitecore.Data;
  using Sitecore.Data.Fields;
  using Sitecore.Data.Items;
  using Sitecore.Globalization;
  using System.Collections;
  using System.Diagnostics;
  using System.Globalization;
  using System.Web;
  using System.Web.SessionState;
  using Sitecore.Data.Managers;


  [UsedImplicitly]
  public class Search : SearchHttpTaskAsyncHandler, IRequiresSessionState
  {
    private static volatile Hashtable cacheHashtable;

    private readonly static object ThisLock;

    private static Hashtable CacheHashTable
    {
      get
      {
        if (Search.cacheHashtable == null)
        {
          lock (Search.ThisLock)
          {
            if (Search.cacheHashtable == null)
            {
              Search.cacheHashtable = new Hashtable();
            }
          }
        }
        return Search.cacheHashtable;
      }
    }

    public override bool IsReusable
    {
      get
      {
        return false;
      }
    }

    static Search()
    {
      Search.ThisLock = new object();
    }

    public Search()
    {
    }

    private static IEnumerable<Tuple<string, string, string>> ProcessCachedDisplayedSearch(SitecoreIndexableItem startLocationItem, IProviderSearchContext searchContext)
    {
      IEnumerable<Tuple<string, string, string>> value;
      string str = string.Concat("IsDisplayedInSearchResults", "[", Context.ContentDatabase.Name, "]");
      Cache item = (Cache)Search.CacheHashTable[str];
      if (item != null)
      {
        value = item.GetValue("cachedIsDisplayedSearch") as IEnumerable<Tuple<string, string, string>>;
      }
      else
      {
        value = null;
      }
      IEnumerable<Tuple<string, string, string>> tuples = value;
      if (tuples == null)
      {
        IQueryable<SitecoreUISearchResultItem> queryable =
            from templateField in searchContext.GetQueryable<SitecoreUISearchResultItem>(new CultureExecutionContext((startLocationItem != null ? startLocationItem.Culture : new CultureInfo(Settings.DefaultLanguage))))
            where templateField["Is Displayed in Search Results".ToLowerInvariant()] == "1"
            select templateField;
        tuples = queryable.ToList<SitecoreUISearchResultItem>().ConvertAll<Tuple<string, string, string>>((SitecoreUISearchResultItem d) => new Tuple<string, string, string>(d.GetItem().ID.ToString(), d.Language, d.Version));
        if (Search.CacheHashTable[str] == null)
        {
          lock (Search.CacheHashTable.SyncRoot)
          {
            if (Search.CacheHashTable[str] == null)
            {
              List<ID> ds = new List<ID>()
                            {
                                new ID(Sitecore.Buckets.Util.Constants.IsDisplayedInSearchResults)
                            };
              item = new DisplayedInSearchResultsCache(str, ds);
              Search.cacheHashtable[str] = item;
            }
          }
        }
        item.Add("cachedIsDisplayedSearch", tuples, Settings.Caching.DefaultFilteredItemsCacheSize);
      }
      return tuples;
    }

    public override void ProcessRequest(HttpContext context)
    {
    }

    public override async Task ProcessRequestAsync(HttpContext context)
    {
      IEnumerable<UISearchResult> document;
      int totalSearchResults;
      Language language;
      Database database;
      ISearchIndex searchIndex;
      int num;
      string str;
      if (ContentSearchManager.Locator.GetInstance<Sitecore.ContentSearch.Utilities.IContentSearchConfigurationSettings>().ItemBucketsEnabled())
      {
        context.Response.ContentType = "application/json";
        context.Response.ContentEncoding = Encoding.UTF8;
        base.Stopwatch = new Stopwatch();
        base.ItemsPerPage = BucketConfigurationSettings.DefaultNumberOfResultsPerPage;
        base.ExtractSearchQuery(context.Request.QueryString);
        base.ExtractSearchQuery(context.Request.Form);
        base.CheckSecurity();
        if (!base.AbortSearch)
        {
          bool flag = MainUtil.GetBool(Sitecore.ContentSearch.Utilities.SearchHelper.GetDebug(base.SearchQuery), false);
          if (flag)
          {
            List<Sitecore.ContentSearch.Utilities.SearchStringModel> searchQuery = base.SearchQuery;
            searchQuery.RemoveAll((Sitecore.ContentSearch.Utilities.SearchStringModel x) => x.Type == "debug");
            if (!BucketConfigurationSettings.EnableBucketDebug)
            {
              Sitecore.Buckets.Util.Constants.EnableTemporaryBucketDebug = true;
            }
          }
          database = (base.Database.IsNullOrEmpty() ? Context.ContentDatabase : Factory.GetDatabase(base.Database));
          Database database1 = database;
          Language language1 = this.Language.IsNullOrEmpty() ? Context.Language : LanguageManager.GetLanguage(this.Language);
          if (base.RunFacet)
          {
            GetFacetsArgs args = new GetFacetsArgs(base.SearchQuery, base.LocationFilter);
            args.CustomData["lang"] = language1.ToString();
            string callback = base.Callback;
            FullSearch fullSearch = new FullSearch()
            {
              PageNumbers = 1,
              facets = GetFacetsPipeline.Run(args),
              SearchCount = "1",
              CurrentPage = 1
            };
            string str1 = string.Concat(callback, "(", JsonConvert.SerializeObject(fullSearch), ")");
            context.Response.Write(str1);
          }
          else
          {
            base.StoreUserContextSearches();
            SitecoreIndexableItem sitecoreIndexableItem = database1.GetItem(base.LocationFilter, language1) ?? database.GetRootItem(language1);
            searchIndex = (base.IndexName.IsEmpty() ? ContentSearchManager.GetIndex(sitecoreIndexableItem) : ContentSearchManager.GetIndex(base.IndexName));
            ISearchIndex searchIndex1 = searchIndex;
            using (IProviderSearchContext providerSearchContext = searchIndex1.CreateSearchContext(SearchSecurityOptions.Default))
            {
              int num1 = int.Parse(base.PageNumber);
              while (true)
              {
                UISearchArgs uISearchArg = new UISearchArgs(providerSearchContext, base.SearchQuery, sitecoreIndexableItem)
                {
                  Page = num1 - 1,
                  PageSize = base.ItemsPerPage
                };
                UISearchArgs uISearchArg1 = uISearchArg;
                base.Stopwatch.Start();
                IQueryable<UISearchResult> uISearchResults = UISearchPipeline.Run(uISearchArg1);
                SearchResults<UISearchResult> results = uISearchResults.GetResults<UISearchResult>();
                IEnumerable<SearchHit<UISearchResult>> hits = results.Hits;
                document =
                    from h in hits
                    select h.Document;
                if (BucketConfigurationSettings.EnableBucketDebug || Sitecore.Buckets.Util.Constants.EnableTemporaryBucketDebug)
                {
                  SearchLog.Log.Info(string.Format("Search Query : {0}", ((IHasNativeQuery)uISearchResults).Query), null);
                  SearchLog.Log.Info(string.Format("Search Index : {0}", searchIndex1.Name), null);
                }
                totalSearchResults = results.TotalSearchResults;
                if (totalSearchResults != 0 || num1 == 1)
                {
                  break;
                }
                num1 = 1;
              }
              List<UISearchResult> list = document.ToList<UISearchResult>();
              num = (totalSearchResults % base.ItemsPerPage == 0 ? Math.Max(totalSearchResults / base.ItemsPerPage, 1) : totalSearchResults / base.ItemsPerPage + 1);
              int num2 = num;
              if ((num1 - 1) * base.ItemsPerPage >= totalSearchResults)
              {
                num1 = 1;
              }
              List<TemplateFieldItem> templateFieldItems = new List<TemplateFieldItem>();
              if (Context.ContentDatabase != null)
              {
                string contextIndexName = ContentSearchManager.GetContextIndexName((IIndexable)(SitecoreIndexableItem)Context.ContentDatabase.GetItem(ItemIDs.TemplateRoot));
                if (contextIndexName != null)
                {
                  using (IProviderSearchContext providerSearchContext1 = ContentSearchManager.GetIndex(contextIndexName).CreateSearchContext(SearchSecurityOptions.Default))
                  {
                    IEnumerable<Tuple<string, string, string>> tuples = Search.ProcessCachedDisplayedSearch(sitecoreIndexableItem, providerSearchContext1);
                    ItemCache itemCache = CacheManager.GetItemCache(Context.ContentDatabase);
                    foreach (Tuple<string, string, string> tuple in tuples)
                    {
                      Sitecore.Globalization.Language.TryParse(tuple.Item2, out language);
                      Item item1 = itemCache.GetItem(new ID(tuple.Item1), language, Sitecore.Data.Version.Parse(tuple.Item3));
                      if (item1 == null)
                      {
                        item1 = Context.ContentDatabase.GetItem(new ID(tuple.Item1), language, Sitecore.Data.Version.Parse(tuple.Item3));
                        if (item1 != null)
                        {
                          CacheManager.GetItemCache(Context.ContentDatabase).AddItem(item1.ID, language, item1.Version, item1);
                        }
                      }
                      if (item1 == null || templateFieldItems.Contains(FieldTypeManager.GetTemplateFieldItem(new Field(item1.ID, item1))))
                      {
                        continue;
                      }
                      templateFieldItems.Add(FieldTypeManager.GetTemplateFieldItem(new Field(item1.ID, item1)));
                    }
                  }
                }
                list = FillItemPipeline.Run(new FillItemArgs(templateFieldItems, list, base.Language, base.ContentLanguage));
              }
              if (base.IndexName == string.Empty)
              {
                List<UISearchResult> uISearchResults1 = list;
                list = uISearchResults1.RemoveWhere<UISearchResult>((UISearchResult item) =>
                {
                  if (item.Name == null)
                  {
                    return true;
                  }
                  return item.Content == null;
                }).ToList<UISearchResult>();
              }
              if (!BucketConfigurationSettings.SecuredItems.Equals("hide", StringComparison.InvariantCultureIgnoreCase))
              {
                if (totalSearchResults > BucketConfigurationSettings.DefaultNumberOfResultsPerPage && list.Count < BucketConfigurationSettings.DefaultNumberOfResultsPerPage && num1 <= num2)
                {
                  while (list.Count < BucketConfigurationSettings.DefaultNumberOfResultsPerPage)
                  {
                    UISearchResult uISearchResult = new UISearchResult()
                    {
                      ItemId = Guid.NewGuid().ToString()
                    };
                    list.Add(uISearchResult);
                  }
                }
                else if (list.Count < totalSearchResults && num1 == 1)
                {
                  while (list.Count < totalSearchResults && totalSearchResults < BucketConfigurationSettings.DefaultNumberOfResultsPerPage)
                  {
                    UISearchResult uISearchResult1 = new UISearchResult()
                    {
                      ItemId = Guid.NewGuid().ToString()
                    };
                    list.Add(uISearchResult1);
                  }
                }
              }
              base.Stopwatch.Stop();
              IEnumerable<Tuple<View, object>> tuples1 = FetchContextDataPipeline.Run(new FetchContextDataArgs(base.SearchQuery, providerSearchContext, sitecoreIndexableItem));
              IEnumerable<Tuple<int, View, string, IEnumerable<UISearchResult>>> tuples2 = FetchContextViewPipeline.Run(new FetchContextViewArgs(base.SearchQuery, providerSearchContext, sitecoreIndexableItem, templateFieldItems));
              string callback1 = base.Callback;
              FullSearch fullSearch1 = new FullSearch()
              {
                PageNumbers = num2,
                items = list,
                launchType = SearchHttpTaskAsyncHandler.GetEditorLaunchType(),
                SearchTime = base.SearchTime,
                SearchCount = totalSearchResults.ToString(),
                ContextData = tuples1,
                ContextDataView = tuples2,
                CurrentPage = num1
              };
              FullSearch fullSearch2 = fullSearch1;
              str = (Context.ContentDatabase.GetItem(base.LocationFilter) != null ? Context.ContentDatabase.GetItem(base.LocationFilter).Name : Translate.Text("current item"));
              fullSearch2.Location = str;
              string str2 = string.Concat(callback1, "(", JsonConvert.SerializeObject(fullSearch1), ")");
              context.Response.Write(str2);
              if (BucketConfigurationSettings.EnableBucketDebug || Sitecore.Buckets.Util.Constants.EnableTemporaryBucketDebug)
              {
                SearchLog.Log.Info(string.Concat("Search Took : ", base.Stopwatch.ElapsedMilliseconds, "ms"), null);
              }
            }
          }
          if (flag)
          {
            Sitecore.Buckets.Util.Constants.EnableTemporaryBucketDebug = false;
          }
        }
      }
    }
  }

  public abstract class SearchHttpTaskAsyncHandler : HttpTaskAsyncHandler
  {
    // Fields
    private bool abortSearch;
    private string pageNumber = "1";
    private List<Sitecore.ContentSearch.Utilities.SearchStringModel> searchQuery = new List<Sitecore.ContentSearch.Utilities.SearchStringModel>();

    // Methods
    protected SearchHttpTaskAsyncHandler()
    {
    }

    public void BuildSearchStringModelFromFieldQuery(System.Collections.Specialized.NameValueCollection queryString, int i)
    {
      foreach (string str in queryString[queryString.Keys[i]].Split(new char[] { ',' }))
      {
        string str2 = str;
        string str3 = queryString.Keys[i];
        string str4 = str3.Replace("-", string.Empty).Replace("+", string.Empty);
        string str5 = str3.StartsWith("-") ? "not" : (str3.StartsWith("+") ? "must" : "should");
        if (str4 == "sort")
        {
          System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"\[(asc|desc)\]$", System.Text.RegularExpressions.RegexOptions.Compiled);
          System.Text.RegularExpressions.Match match = regex.Match(str);
          if (match.Success)
          {
            str5 = match.Value;
            str2 = regex.Replace(str, string.Empty);
          }
        }
        Sitecore.ContentSearch.Utilities.SearchStringModel item = new Sitecore.ContentSearch.Utilities.SearchStringModel
        {
          Operation = str5,
          Type = str4,
          Value = str2
        };
        this.searchQuery.Add(item);
      }
    }

    public void BuildSearchStringModelFromQuery(System.Collections.Specialized.NameValueCollection queryString, int i)
    {
      if (((queryString[queryString.Keys[i]] != "Query") && (queryString.Keys[i] != "_")) && (queryString.Keys[i + 1] != "_"))
      {
        Sitecore.ContentSearch.Utilities.SearchStringModel item = new Sitecore.ContentSearch.Utilities.SearchStringModel
        {
          Type = queryString[queryString.Keys[i]],
          Value = queryString[queryString.Keys[i + 1]],
          Operation = (queryString.Keys.Count > 2) ? queryString[queryString.Keys[i + 2]] : "must"
        };
        this.searchQuery.Add(item);
      }
    }

    public void CheckSecurity()
    {
      HttpContext current = HttpContext.Current;
      if ((!Context.User.IsAuthenticated || !Sitecore.Web.Authentication.TicketManager.IsCurrentTicketValid()) && (current != null))
      {
        current.Response.Write(this.Callback + "(" + JsonConvert.SerializeObject(new { Redirect = Context.Site.LoginPage }) + ")");
        current.Response.End();
        this.AbortSearch = true;
      }
    }

    public void ExtractFacetTagFromSearchQuery(System.Collections.Specialized.NameValueCollection parameters)
    {
      if (parameters["type"] != null)
      {
        this.RunFacet = parameters["type"] != "Query";
      }
    }

    public void ExtractPageNumberFromQuery(System.Collections.Specialized.NameValueCollection parameters, int i)
    {
      this.PageNumber = parameters[parameters.Keys[i]];
      if (this.PageNumber == "0")
      {
        this.PageNumber = "1";
      }
    }

    public void ExtractPageSizeFromQuery(System.Collections.Specialized.NameValueCollection parameters, int i)
    {
      int defaultNumberOfResultsPerPage;
      string s = parameters[parameters.Keys[i]];
      if (!int.TryParse(s, out defaultNumberOfResultsPerPage))
      {
        defaultNumberOfResultsPerPage = BucketConfigurationSettings.DefaultNumberOfResultsPerPage;
      }
      if (defaultNumberOfResultsPerPage == 0)
      {
        defaultNumberOfResultsPerPage = 0x7fffffff;
      }
      this.ItemsPerPage = defaultNumberOfResultsPerPage;
    }

    public void ExtractSearchQuery(System.Collections.Specialized.NameValueCollection parameters)
    {
      if (parameters.Keys.Count > 0)
      {
        bool flag = !string.IsNullOrEmpty(parameters["fromBucketListField"]);
        this.ExtractFacetTagFromSearchQuery(parameters);
        for (int i = 0; i < parameters.Keys.Count; i++)
        {
          switch (parameters.Keys[i])
          {
            case "sc_content":
              break;

            case "callback":
              this.Callback = System.Text.RegularExpressions.Regex.Replace(parameters[parameters.Keys[i]], @"[^\w\d_$]", string.Empty, System.Text.RegularExpressions.RegexOptions.Compiled);
              break;

            case "version":
              this.Version = parameters[parameters.Keys[i]];
              break;

            case "pageSize":
              this.ExtractPageSizeFromQuery(parameters, i);
              break;

            case "pageNumber":
              this.ExtractPageNumberFromQuery(parameters, i);
              break;

            case "scLanguage":
              this.ContentLanguage = parameters[parameters.Keys[i]];
              break;

            case "StartSearchLocation":
              {
                Sitecore.ContentSearch.Utilities.SearchStringModel item = new Sitecore.ContentSearch.Utilities.SearchStringModel
                {
                  Type = "location",
                  Value = parameters[i],
                  Operation = "must"
                };
                this.searchQuery.Add(item);
                break;
              }
            case "fromBucketListField":
              if (parameters[i] != "*")
              {
                Sitecore.ContentSearch.Utilities.SearchStringModel model = new Sitecore.ContentSearch.Utilities.SearchStringModel
                {
                  Type = "text",
                  Value = parameters[i],
                  Operation = "must"
                };
                this.searchQuery.Add(model);
              }
              break;

            case "filterText":
              {
                Sitecore.ContentSearch.Utilities.SearchStringModel model3 = new Sitecore.ContentSearch.Utilities.SearchStringModel
                {
                  Type = "text",
                  Value = parameters[i],
                  Operation = "must"
                };
                this.searchQuery.Add(model3);
                break;
              }
            default:
              if (flag)
              {
                this.BuildSearchStringModelFromFieldQuery(parameters, i);
              }
              else
              {
                this.BuildSearchStringModelFromQuery(parameters, i);
                i += 2;
              }
              break;
          }
        }
      }
    }

    public static string GetEditorLaunchType()
    {
      Field field = Sitecore.Buckets.Util.Constants.SettingsItem.Fields[Sitecore.Buckets.Util.Constants.OpenSearchResultFieldName];
      if ((field != null) && (field.Value == "New Tab"))
      {
        return "contenteditor:launchtab";
      }
      return "search:launchresult";
    }

    public static List<Tip> GetRandomTips(int count)
    {
      List<Tip> list = new List<Tip>();
      Tip item = new Tip
      {
        TipName = "Could Not Retrieve Tips",
        TipText = "Could Not Retrieve Tips"
      };
      list.Add(item);
      return list;
    }

    public void StoreUserContextSearches()
    {
      string str = (from query in this.searchQuery
                    where !string.IsNullOrEmpty(query.Value)
                    select query).Aggregate<Sitecore.ContentSearch.Utilities.SearchStringModel, string>(string.Empty, (current, query) => current + query.ToString() + ";");
      if (ClientContext.GetValue("Searches") == null)
      {
        ClientContext.SetValue("Searches", string.Empty);
      }
      object obj2 = ClientContext.GetValue("Searches");
      if ((obj2 != null) && !obj2.ToString().Contains("~" + str + "~"))
      {
        ClientContext.SetValue("Searches", string.Concat(new object[] { ClientContext.GetValue("Searches"), "~", str, "~" }));
      }
    }

    // Properties
    public bool AbortSearch
    {
      get
      {
        return this.abortSearch;
      }
      set
      {
        this.abortSearch = value;
      }
    }

    protected string Callback { get; set; }

    public string ContentLanguage { get; set; }

    public string Database
    {
      get
      {
        Uri urlReferrer = HttpContext.Current.Request.UrlReferrer;
        if (urlReferrer == null)
        {
          return string.Empty;
        }
        if (!string.IsNullOrEmpty(Sitecore.Web.WebUtil.GetQueryString("db")))
        {
          return Sitecore.Web.WebUtil.GetQueryString("db");
        }
        return Sitecore.Web.WebUtil.ExtractUrlParm("db", urlReferrer.AbsoluteUri);
      }
    }

    public string IndexName
    {
      get
      {
        return Sitecore.Web.WebUtil.GetQueryString("indexName");
      }
    }

    protected int ItemsPerPage { get; set; }

    public string Language
    {
      get
      {
        Uri urlReferrer = HttpContext.Current.Request.UrlReferrer;
        if (urlReferrer == null)
        {
          return string.Empty;
        }
        if (!string.IsNullOrEmpty(Sitecore.Web.WebUtil.GetQueryString("la")))
        {
          return Sitecore.Web.WebUtil.ExtractUrlParm("la", urlReferrer.AbsoluteUri);
        }
        return Sitecore.Web.WebUtil.ExtractUrlParm("la", urlReferrer.AbsoluteUri);
      }
    }

    public string LocationFilter
    {
      get
      {
        string str = null;
        ID rootID;
        HttpRequest request = HttpContext.Current.Request;
        Uri urlReferrer = request.UrlReferrer;
        if (urlReferrer != null)
        {
          str = HttpUtility.ParseQueryString(urlReferrer.Query)["id"];
        }
        str = (request.Form["StartSearchLocation"] ?? request.QueryString["StartSearchLocation"]) ?? str;
        try
        {
          rootID = ID.Parse(str);
        }
        catch (Exception)
        {
          Sitecore.Diagnostics.Log.Error(string.Concat(new object[] { "Value of the StartSearchLocation '", str, "' is not a valid GUID. '", ItemIDs.RootID, "' will be used instead." }), this);
          rootID = ItemIDs.RootID;
        }
        return rootID.ToString();
      }
    }

    protected string PageNumber
    {
      get
      {
        return this.pageNumber;
      }
      set
      {
        this.pageNumber = value;
      }
    }

    protected bool RunFacet { get; set; }

    protected List<Sitecore.ContentSearch.Utilities.SearchStringModel> SearchQuery
    {
      get
      {
        return this.searchQuery;
      }
    }

    public string SearchTime
    {
      get
      {
        return this.Stopwatch.Elapsed.ToString(@"ss\.ffff");
      }
    }

    protected Stopwatch Stopwatch { get; set; }

    protected string Version { get; set; }
  }
}
