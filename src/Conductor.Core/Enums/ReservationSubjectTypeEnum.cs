namespace Conductor.Core.Enums
{
    /// <summary>
    /// Subject type allowed to use a reserved virtual model runner.
    /// </summary>
    public enum ReservationSubjectTypeEnum
    {
        /// <summary>
        /// Unknown subject type.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// User identity.
        /// </summary>
        User = 1,

        /// <summary>
        /// Credential identity.
        /// </summary>
        Credential = 2
    }
}
