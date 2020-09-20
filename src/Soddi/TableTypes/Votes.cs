#nullable disable
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

using System;

namespace Soddi.TableTypes
{
    [StackOverflowDataTable("votes.xml")]
    public class Votes
    {
        public int? BountyAmount { get; set; }
        public DateTime CreationDate { get; set; }
        public int Id { get; set; }
        public int PostId { get; set; }
        public int? UserId { get; set; }
        public int VoteTypeId { get; set; }
    }
}
