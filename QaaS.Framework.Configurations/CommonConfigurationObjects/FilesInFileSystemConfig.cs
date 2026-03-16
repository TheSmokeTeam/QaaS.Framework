using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using QaaS.Framework.Configurations.CustomValidationAttributes;

namespace QaaS.Framework.Configurations.CommonConfigurationObjects;

/// <summary>
/// Configuration to files in the file system
/// </summary>
public record FilesInFileSystemConfig : IStorageConfig
{
    [Required, ValidPath, Description("The path of the directory containing the relevant files")]
    public string? Path { get; set; }
    
    [Description("The search string to match against the names of files in path." +
                 " This parameter can contain a combination of valid literal path and wildcard" +
                 " (* and ?) characters, but it doesn't support regular expressions."),
     DefaultValue("")]
    public string SearchPattern { get; set; } = "";
}
