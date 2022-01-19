#nullable disable
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Soddi.TableTypes;

[StackOverflowDataTable("users.xml")]
public class Users
{
    public string AboutMe { get; set; }
    public int? Age { get; set; }
    public DateTime CreationDate { get; set; }
    public string DisplayName { get; set; }
    public int DownVotes { get; set; }
    public string EmailHash { get; set; }
    public int Id { get; set; }
    public DateTime LastAccessDate { get; set; }
    public string Location { get; set; }
    public int Reputation { get; set; }
    public int UpVotes { get; set; }
    public int Views { get; set; }
    public string WebsiteUrl { get; set; }
    public int AccountId { get; set; }
}