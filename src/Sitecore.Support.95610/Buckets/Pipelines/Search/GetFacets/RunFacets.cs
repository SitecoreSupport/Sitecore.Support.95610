namespace Sitecore.Support.Buckets.Pipelines.Search.GetFacets
{
  using ContentSearch.SearchTypes;
  using Data.Managers;
  using Globalization;
  using Sitecore;
  using Sitecore.Buckets.Pipelines.Search.GetFacets;
  using Sitecore.Buckets.Search;
  using Sitecore.Buckets.Util;
  using Sitecore.ContentSearch;
  using Sitecore.ContentSearch.Diagnostics;
  using Sitecore.ContentSearch.Linq.Parsing;
  using Sitecore.ContentSearch.Security;
  using Sitecore.ContentSearch.Utilities;
  using Sitecore.Data.Items;
  using System;
  using System.Collections.Generic;
  using System.Diagnostics;
  using System.Linq;

  public class RunFacets : GetFacetsProcessor
  {
    public override void Process(GetFacetsArgs args)
    {
      if (args.Result == null)
      {
        List<SearchStringModel> searchQuery = args.SearchQuery;
        if ((searchQuery != null) && (Context.ContentDatabase != null))
        {
          Language lang = LanguageManager.GetLanguage(args.CustomData["lang"].ToString()) ?? Context.Language;
          SitecoreIndexableItem indexable = Context.ContentDatabase.GetItem(args.LocationFilter, lang);
          string contextIndexName = ContentSearchManager.GetContextIndexName(indexable);
          using (IProviderSearchContext context = ContentSearchManager.GetIndex(contextIndexName).CreateSearchContext(SearchSecurityOptions.Default))
          {
            IQueryable<UISearchResult> query = LinqHelper.CreateQuery<UISearchResult>(context, searchQuery, (Item)indexable, null);
            if (BucketConfigurationSettings.EnableBucketDebug || Sitecore.Buckets.Util.Constants.EnableTemporaryBucketDebug)
            {
              SearchLog.Log.Info("Facet Search Query : " + ((IHasNativeQuery)query).Query.ToString(), null);
              SearchLog.Log.Info("Facet Search Index : " + contextIndexName, null);
            }
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            args.Result = (IEnumerable<IEnumerable<SitecoreUIFacet>>)FacetSearcher.GetFacets(query, args.LocationFilter, context, Context.ContentDatabase);
            stopwatch.Stop();
            if (BucketConfigurationSettings.EnableBucketDebug || Sitecore.Buckets.Util.Constants.EnableTemporaryBucketDebug)
            {
              SearchLog.Log.Info("Facet Search Took : " + stopwatch.ElapsedMilliseconds + "ms", null);
            }
          }
        }
      }
    }
  }
}
