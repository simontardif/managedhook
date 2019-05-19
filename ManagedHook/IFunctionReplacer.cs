
namespace ManagedHook
{
    /// <summary>
    /// The user must implements this interface for the hooked method
    /// </summary>
    public interface IFunctionReplacer
    {
        /// <summary>
        /// This method is called instead of the hooked method
        /// </summary>
        /// <param name="instanceHooked">The instance hooked, null if the hooked method is static</param>
        /// <param name="parameters">The hooked method parameters</param>
        void NewFunction(object instanceHooked, object[] parameters);
    }
}
