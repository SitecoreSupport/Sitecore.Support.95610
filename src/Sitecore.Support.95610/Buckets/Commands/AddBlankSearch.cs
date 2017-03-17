namespace Sitecore.Support.Buckets.Commands
{
  using Sitecore;
  using Sitecore.Buckets.Commands;
  using Sitecore.Buckets.Util;
  using Sitecore.Diagnostics;
  using Sitecore.Globalization;
  using Sitecore.Resources;
  using Sitecore.Shell.Framework.Commands;
  using Sitecore.Text;
  using Sitecore.Web;
  using Sitecore.Web.UI.Framework.Scripts;
  using Sitecore.Web.UI.Sheer;
  using System;
  using System.Globalization;
  using System.Linq;
  using System.Text.RegularExpressions;

  [Serializable]
  public class AddBlankSearch : BaseCommand, IItemBucketsCommand
  {
    internal UrlString urlString;

    public override void Execute(CommandContext context)
    {
      Assert.ArgumentNotNull(context, "context");
      if (context.Items.Length == 1)
      {
        string str = context.Parameters[0];
        this.urlString = new UrlString(Sitecore.Buckets.Util.Constants.SearchApplicationPath);
        this.urlString.Add(Sitecore.Buckets.Util.Constants.OpenItemEditorQueryStringKeyName, str);
        context.Items[0].Uri.AddToUrlString(this.urlString);
        UIUtil.AddContentDatabaseParameter(this.urlString);
        this.urlString.Add("il", "0");
        this.urlString.Add(Sitecore.Buckets.Util.Constants.ModeQueryStringKeyName, "preview");
        this.urlString.Add(Sitecore.Buckets.Util.Constants.RibbonQueryStringKeyName, Sitecore.Buckets.Util.Constants.BlankSearchEditorId);
        //string str2 = context.Parameters["la"] ?? Context.Language.CultureInfo.TwoLetterISOLanguageName;
        string str2 = context.Parameters["la"] ?? context.Items[0].Language.ToString();
        this.urlString.Add("la", str2);
        this.urlString.Add("sc_language", Context.Language.Name);
        SheerResponse.Eval(new ShowEditorTab
        {
          Command = "contenteditor:launchblanktab",
          Header = string.Concat(new object[] {
                        Translate.Text("Search"),
                        " [",
                        this.GetSearchTabNumber(),
                        "]"
                    }),
          Icon = Images.GetThemedImageSource("Applications/16x16/text_view.png"),
          Url = this.urlString.ToString(),
          Id = new Random().Next(0, 0x5f5e0ff).ToString(CultureInfo.InvariantCulture),
          Closeable = true,
          Activate = true
        }.ToString());
      }
    }

    protected int GetSearchTabNumber()
    {
      int[] numArray = (from match in from s in WebUtil.GetFormValue("scEditorTabs").Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries) select new Regex(@"\[([0-9]{1,2})\]$").Match(s.Split(new char[] { '^' })[1])
                        where match.Success
                        select int.Parse(match.Groups[1].Value) into i
                        orderby i
                        select i).Distinct<int>().ToArray<int>();
      int num = 1;
      while ((numArray.Length != (num - 1)) && (num == numArray[num - 1]))
      {
        num++;
      }
      return num;
    }

    public override CommandState QueryState(CommandContext context)
    {
      Assert.ArgumentNotNull(context, "context");
      if (context.Items.Length != 1)
      {
        return CommandState.Disabled;
      }
      if (base.HasField(context.Items[0], FieldIDs.LayoutField))
      {
        return base.QueryState(context);
      }
      return CommandState.Hidden;
    }
  }
}
