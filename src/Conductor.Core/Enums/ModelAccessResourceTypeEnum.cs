namespace Conductor.Core.Enums
{
    /// <summary>
    /// Resource type matched by a model access policy rule.
    /// </summary>
    public enum ModelAccessResourceTypeEnum
    {
        /// <summary>
        /// Match a model definition identifier.
        /// </summary>
        ModelDefinition = 0,

        /// <summary>
        /// Match a requested or effective model name.
        /// </summary>
        ModelName = 1,

        /// <summary>
        /// Match labels assigned to a model definition.
        /// </summary>
        ModelLabel = 2,

        /// <summary>
        /// Match a virtual model runner identifier.
        /// </summary>
        VirtualModelRunner = 3,

        /// <summary>
        /// Match any model access resource.
        /// </summary>
        Any = 4
    }
}
