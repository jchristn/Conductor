namespace Conductor.Core.Enums
{
    using System;

    /// <summary>
    /// Enumeration order enumeration.
    /// </summary>
    public enum EnumerationOrderEnum
    {
        /// <summary>
        /// Order by creation timestamp ascending.
        /// </summary>
        CreatedAscending = 0,

        /// <summary>
        /// Order by creation timestamp descending.
        /// </summary>
        CreatedDescending = 1,

        /// <summary>
        /// Order by last update timestamp ascending.
        /// </summary>
        LastUpdateAscending = 2,

        /// <summary>
        /// Order by last update timestamp descending.
        /// </summary>
        LastUpdateDescending = 3,

        /// <summary>
        /// Order by name ascending.
        /// </summary>
        NameAscending = 4,

        /// <summary>
        /// Order by name descending.
        /// </summary>
        NameDescending = 5
    }
}
