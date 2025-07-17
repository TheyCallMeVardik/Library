using Newtonsoft.Json;

namespace Library.DTO
{
    public class BookSearchDto
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        [JsonProperty("ISBN")]
        public string isbn { get; set; }
        public int PublicationYear { get; set; }
        public string ImageUrl { get; set; }
        public string Description { get; set; }
    }
}
