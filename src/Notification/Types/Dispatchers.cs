using SharedLibs.Types;

namespace Notification.Types;

public interface IDispatchers
{
  public IDispatcher? GetDispatcher(string protocol);
}

public interface IDispatcher
{
  public Task Dispatch(NotifData data, string destination, Action<bool> callback);
}