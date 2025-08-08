using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using System.Text;

namespace AdPlatformService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PlatformsController : ControllerBase
    {
        private static ConcurrentDictionary<string, List<string>> _locationIndex = new();

        // POST /api/platforms
        [HttpPost]
        public IActionResult Upload([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
                return StatusCode(500, "500 Error: Файл пустой или не предоставлен.");

            try
            {
                var newIndex = new ConcurrentDictionary<string, List<string>>();
                using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);

                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line) || !line.Contains(':'))
                        continue;

                    var parts = line.Split(':', 2);
                    if (parts.Length != 2) continue;

                    var platformName = parts[0].Trim();
                    var locations = parts[1]
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(l => l.Trim()) // убираем пробелы после запятых
                        .Where(l => !string.IsNullOrWhiteSpace(l))
                        .ToList();

                    foreach (var location in locations)
                    {
                        newIndex.AddOrUpdate(location,
                            _ => new List<string> { platformName },
                            (_, list) =>
                            {
                                if (!list.Contains(platformName))
                                    list.Add(platformName);
                                return list;
                            });
                    }
                }

                _locationIndex = newIndex;
                return StatusCode(201, "201 Created: Файл загружен и данные успешно сохранены.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"500 Error: Внутренняя ошибка сервера, {ex.Message}");
            }
        }

        // GET /api/platforms/{*search}
        [HttpGet("{*search}")]
        public IActionResult Search([FromRoute] string search)
        {
            if (string.IsNullOrWhiteSpace(search))
                return BadRequest("400 Bad Request: Локация для поиска обязательна.");

            var results = new HashSet<string>();

            search = Uri.UnescapeDataString(search);

            search = search.Trim('/');

            foreach (var kvp in _locationIndex)
            {
                var normalizedKey = kvp.Key.Trim('/');

                bool isMatch =
                    normalizedKey.StartsWith(search, StringComparison.OrdinalIgnoreCase) || search.StartsWith(normalizedKey, StringComparison.OrdinalIgnoreCase);

                if (isMatch)
                {
                    foreach (var platform in kvp.Value)
                        results.Add(platform);
                }
            }

            if (results.Count == 0)
            {
                return StatusCode(204, new
                {
                    status = "204 No Content: Поиск выполнен успешно.",
                    location = search,
                    platforms = new List<string>(),
                    message = "Для указанной локации не найдено рекламных площадок."
                });
            }

            return Ok(new
            {
                status = "200 Success: Поиск выполнен успешно.",
                location = search,
                platforms = results.OrderBy(p => p).ToList()
            });
        }

    }
}