namespace Sitecore.Support.Buckets.Commands
{
  using Sitecore.Data.Items;
  using Sitecore.Diagnostics;
  using Sitecore.Shell.Framework.Commands;
  using System;

  [Serializable]
  public abstract class BaseCommand : Command
  {
    protected BaseCommand()
    {
    }

    public abstract override void Execute(CommandContext context);
    public virtual CommandState QueryState(CommandContext context)
    {
      Assert.ArgumentNotNull(context, "context");
      Item item = context.Items[0];
      if (item != null)
      {
        if (context.Items.Length == 0)
        {
          return CommandState.Disabled;
        }
        if (item.Appearance.ReadOnly)
        {
          return CommandState.Disabled;
        }
        if (!item.Access.CanWrite())
        {
          return CommandState.Disabled;
        }
        if (Command.IsLockedByOther(item))
        {
          return CommandState.Disabled;
        }
      }
      return base.QueryState(context);
    }
  }
}
