// using MarkdownGenQAs.Application;
using MarkdownGenQAs.Interfaces.Repository;
using MarkdownGenQAs.Models.DB;

namespace MarkdownGenQAs.Repositories;

public class CategoryFileRepository : GenericRepository<CategoryFile>, ICategoryFileRepository
{
    public CategoryFileRepository(ApplicationContext context) : base(context)
    {
    }
}
