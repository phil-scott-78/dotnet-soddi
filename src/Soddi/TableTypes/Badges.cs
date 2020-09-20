#nullable disable
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

using System;

namespace Soddi.TableTypes
{
    [StackOverflowDataTable("badges.xml")]
    public class Badges
    {
        public DateTime Date { get; set; }
        public int Id { get; set; }
        public string Name { get; set; }
        public int UserId { get; set; }
    }
}
