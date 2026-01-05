// using MarkdownGenQAs.Application;
using MarkdownGenQAs.Interfaces.Repository;
using MarkdownGenQAs.Models.DB;

namespace MarkdownGenQAs.Repositories;

public class LogMessageRepository : GenericRepository<LogMessage>, ILogMessageRepository
{
    public LogMessageRepository(ApplicationContext context) : base(context)
    {
    }
}
