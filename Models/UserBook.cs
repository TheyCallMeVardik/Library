namespace Library.Models
{
    public class UserBook
    {
        public int Id { get; set; }
        public string UserId { get; set; } 
        public int BookId { get; set; }
        public DateTime PurchaseDate { get; set; }

        public Books Book { get; set; }
        public ApplicationUser ApplicationUser { get; set; }
    }
}
