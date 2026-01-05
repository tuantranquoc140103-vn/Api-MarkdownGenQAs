// using MarkdownGenQAs.Application;
using MarkdownGenQAs.Interfaces.Repository;
using MarkdownGenQAs.Models.DB;

namespace MarkdownGenQAs.Repositories;

public class FileMetadataRepository : GenericRepository<FileMetadata>, IFileMetadataRepository
{
    public FileMetadataRepository(ApplicationContext context) : base(context)
    {
    }
}
