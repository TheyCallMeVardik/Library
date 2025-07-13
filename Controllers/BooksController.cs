using Library.Data;
using Library.DTO;
using Library.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nest;
using Elasticsearch.Net;
using System.Globalization;

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
                PublicationYear = book.PublicationYear
            };

            var response = await _elasticClient.IndexDocumentAsync(dto);
            if (!response.IsValid)
                return BadRequest(response.OriginalException?.Message ?? "Ошибка индексации");

            return CreatedAtAction(nameof(GetBooks), new { id = book.Id }, book);
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
                PublicationYear = existing.PublicationYear
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

        // Поиск книг
        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<BookSearchDto>>> SearchBooks(
    string? query = null,
    string sortBy = "title",
    bool ascending = true)
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
                .Sort(st =>
                    sortBy.ToLower() == "year"
                    ? st.Field(f => f.PublicationYear, ascending ? SortOrder.Ascending : SortOrder.Descending)
                    : st.Field("title.keyword", ascending ? SortOrder.Ascending : SortOrder.Descending)
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


    }
}
