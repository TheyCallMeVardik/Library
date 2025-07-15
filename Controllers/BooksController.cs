using Library.Data;
using Library.DTO;
using Library.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nest;
using Elasticsearch.Net;
using System.Security.Claims;

namespace Library.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BooksController : ControllerBase
    {
        private readonly LibraryDbContext _context;
        private readonly IElasticClient _elasticClient;

        public BooksController(LibraryDbContext context, IElasticClient elasticClient)
        {
            _context = context;
            _elasticClient = elasticClient;
        }

        // Получение всех книг
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Books>>> GetBooks()
            => await _context.Books.ToListAsync();

        // Добавление книги
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<Books>> AddBook(Books book)
        {
            _context.Books.Add(book);
            await _context.SaveChangesAsync();

            var dto = new BookSearchDto
            {
                Id = book.Id,
                Title = book.Title,
                Author = book.Author,
                ISBN = book.ISBN,
                PublicationYear = book.PublicationYear,
            };

            var response = await _elasticClient.IndexDocumentAsync(dto);
            if (!response.IsValid)
                return BadRequest(response.OriginalException?.Message ?? "Ошибка индексации");

            return CreatedAtAction(nameof(GetBooks), new { id = book.Id }, book);
        }

        [HttpPost("bulk")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddBooksBulk([FromBody] List<Books> books)
        {
            if (books == null || !books.Any())
                return BadRequest("No books provided");

            _context.Books.AddRange(books);
            await _context.SaveChangesAsync();

            foreach (var book in books)
            {
                var dto = new BookSearchDto
                {
                    Id = book.Id,
                    Title = book.Title,
                    Author = book.Author,
                    ISBN = book.ISBN,
                    PublicationYear = book.PublicationYear,
                };
                var response = await _elasticClient.IndexDocumentAsync(dto);
                if (!response.IsValid)
                    return StatusCode(500, $"Ошибка индексации для книги {book.Title}: {response.OriginalException?.Message}");
            }

            return CreatedAtAction(nameof(GetBooks), new { }, books);
        }

        // Обновление книги
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateBook(int id, Books updatedBook)
        {
            if (id != updatedBook.Id)
                return BadRequest("ID mismatch");

            var existing = await _context.Books.FindAsync(id);
            if (existing == null)
                return NotFound();

            existing.Title = updatedBook.Title;
            existing.Author = updatedBook.Author;
            existing.ISBN = updatedBook.ISBN;
            existing.PublicationYear = updatedBook.PublicationYear;

            _context.Entry(existing).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            var dto = new BookSearchDto
            {
                Id = existing.Id,
                Title = existing.Title,
                Author = existing.Author,
                ISBN = existing.ISBN,
                PublicationYear = existing.PublicationYear,
            };

            var response = await _elasticClient.IndexDocumentAsync(dto);
            if (!response.IsValid)
                return BadRequest(response.OriginalException?.Message ?? "Ошибка обновления индекса");

            return NoContent();
        }

        // Удаление книги
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteBook(int id)
        {
            var book = await _context.Books.FindAsync(id);
            if (book == null)
                return NotFound();

            _context.Books.Remove(book);
            await _context.SaveChangesAsync();

            var response = await _elasticClient.DeleteAsync<BookSearchDto>(id.ToString(), d => d
                .Index("books")
                .Refresh(Refresh.True)
            );

            if (!response.IsValid)
                return StatusCode(500, response.OriginalException?.Message ?? "Ошибка удаления из индекса");

            return NoContent();
        }

        [HttpDelete]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteAllBooks()
        {
            var books = await _context.Books.ToListAsync();
            if (!books.Any()) return NoContent();

            _context.Books.RemoveRange(books);
            await _context.SaveChangesAsync();

            var deleteResponse = await _elasticClient.DeleteByQueryAsync<BookSearchDto>(d => d
                .Index("books")
                .Query(q => q.MatchAll())
                .Refresh(true)
            );

            if (!deleteResponse.IsValid)
                return StatusCode(500, deleteResponse.OriginalException?.Message ?? "Ошибка массового удаления из индекса");

            return NoContent();
        }

        // Поиск книг
        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<BookSearchDto>>> SearchBooks(string? query = null)
        {
            var response = await _elasticClient.SearchAsync<BookSearchDto>(s => s
                .Index("books")
                .Query(q =>
                    string.IsNullOrWhiteSpace(query)
                    ? q.MatchAll()
                    : q.Bool(b => b
                        .Should(
                            sh => sh.MatchPhrasePrefix(mpp => mpp.Field(f => f.Title).Query(query)),
                            sh => sh.MatchPhrasePrefix(mpp => mpp.Field(f => f.Author).Query(query))
                        )
                        .MinimumShouldMatch(1)
                    )
                )
            );

            if (!response.IsValid)
            {
                return BadRequest(new
                {
                    response.ServerError?.Error?.Type,
                    response.ServerError?.Error?.Reason,
                    response.ServerError?.Error?.CausedBy
                });
            }

            return Ok(response.Documents);
        }
        [HttpPost("purchase")]
        [Authorize]
        public async Task<ActionResult<UserBook>> PurchaseBook(int bookId)
        {
            var book = await _context.Books.FindAsync(bookId);
            if (book == null) return NotFound("Book not found");

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var existingPurchase = await _context.UserBooks
                .FirstOrDefaultAsync(ub => ub.UserId == userId && ub.BookId == bookId);
            if (existingPurchase != null) return BadRequest("Book already purchased");

            var userBook = new UserBook
            {
                UserId = userId,
                BookId = bookId,
                PurchaseDate = DateTime.UtcNow
            };

            _context.UserBooks.Add(userBook);
            await _context.SaveChangesAsync();

            var dto = new BookSearchDto
            {
                Id = book.Id,
                Title = book.Title,
                Author = book.Author,
                ISBN = book.ISBN,
                PublicationYear = book.PublicationYear
            };
            var indexResponse = await _elasticClient.IndexDocumentAsync(dto);
            if (!indexResponse.IsValid)
                return StatusCode(500, "Ошибка обновления индекса");

            return CreatedAtAction(nameof(PurchaseBook), new { id = userBook.Id }, userBook);
        }

        [HttpGet("my-books")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<UserBook>>> GetMyBooks()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var userBooks = await _context.UserBooks
                .Where(ub => ub.UserId == userId)
                .Include(ub => ub.Book)
                .ToListAsync();
            return Ok(userBooks);
        }
    }
}
