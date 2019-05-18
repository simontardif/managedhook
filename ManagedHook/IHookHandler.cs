
namespace ManagedHook
{
    /// <summary>
    /// The user must implements this interface for the hooked method
    /// </summary>
    public interface IHookHandler
    {
        /// <summary>
        /// This method is called before the hooked method
        /// </summary>
        /// <param name="instanceHooked">The instance hooked, null if the hooked method is static</param>
        void Before(object instanceHooked);

        /// <summary>
        /// This method is called after the hooked method
        /// </summary>
        /// <param name="instanceHooked">The instance hooked, null if the hooked method is static</param>
        void After(object instanceHooked);
    }
}
