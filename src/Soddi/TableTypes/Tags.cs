#nullable disable
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Soddi.TableTypes
{
    [StackOverflowDataTable("tags.xml")]
    public class Tags
    {
        public int Id { get; set; }
        public string TagName { get; set; }
        public int Count { get; set; }
        public int? ExcerptPostId { get; set; }
        public int? WikiPostId { get; set; }
    }
}
