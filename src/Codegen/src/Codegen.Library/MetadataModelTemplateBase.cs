using CSharpRazor;

namespace Codegen.Library
{
    // TODO: Move to dotnet-cgcsharp project (this type is only used in razor templates)
    /// <summary>
    /// Codegen specific template class that makes the model into a <see cref="MetadataModelTemplateBase{TRecord}"/>
    /// </summary>
    /// <typeparam name="TRecord">The type used for database records.</typeparam>
    public abstract class MetadataModelTemplateBase<TRecord> : TemplateBase<MetadataModel<TRecord>>
    {
    }
}
