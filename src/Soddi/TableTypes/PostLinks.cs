#nullable disable
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Soddi.TableTypes;

[StackOverflowDataTable("postlinks.xml")]
public class PostLinks
{
    public int Id { get; set; }
    public DateTime CreationDate { get; set; }
    public int PostId { get; set; }
    public int RelatedPostId { get; set; }
    public short LinkTypeId { get; set; }
}