namespace WondrousTailsSolver
{
    /// <summary>
    /// The state of a sticker button.
    /// </summary>
    public enum ButtonState
    {
        /// <summary>
        /// Needs instance completion to become available .
        /// </summary>
        Completable,

        /// <summary>
        /// Can click button to get a stamp right now.
        /// </summary>
        AvailableNow,

        /// <summary>
        /// Already completed, needs re-roll.
        /// </summary>
        Unavailable,

        /// <summary>
        /// Data is state, unknown state.
        /// </summary>
        Unknown,
    }
}
