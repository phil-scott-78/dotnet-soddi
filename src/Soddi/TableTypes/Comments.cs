#nullable disable
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Soddi.TableTypes;

[StackOverflowDataTable("comments.xml")]
public class Comments
{
    public DateTime CreationDate { get; set; }
    public int Id { get; set; }
    public int PostId { get; set; }
    public int? Score { get; set; }
    public string Text { get; set; }
    public int? UserId { get; set; }
    public string ContentLicense { get; set; }
}