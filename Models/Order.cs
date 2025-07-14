namespace Library.Models
{
    public class Order
    {
        public int Id { get; set; }
        public int BookId { get; set; }
        public string UserId { get; set; } 
        public DateTime OrderDate { get; set; }
        public Books Book { get; set; }
        public ApplicationUser User { get; set; }
    }
}
