
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Microsoft.Extensions.Options;
using Sprache;

public class MarkdownService : IMarkdownService
{
    private readonly TokenCountService _tokenCountService;
    private TypeChunk typeChunk = TypeChunk.Text;
    private readonly ChunkOption _chunkOption;
    private readonly MarkdownPipeline _pipeline;
    private readonly ILlmServiceFactory _llmProviderFactory;
    private readonly SystemPrompts _systemPrompts;
    private readonly ILogger<MarkdownService> _logger;

    private Stack<KeyValuePair<int, string>> _stackHeaderTitle = new Stack<KeyValuePair<int, string>>();

    public MarkdownService(TokenCountService tokenCountService, IOptions<ChunkOption> options,
                            ILlmServiceFactory llmProviderFactory, MarkdownPipeline pipeline,
                            IOptions<SystemPrompts> systemPrompts, ILogger<MarkdownService> logger)
    {
        _tokenCountService = tokenCountService;
        _chunkOption = options.Value ?? throw new ArgumentNullException("ChunkOption is missing in appsettings.json");
        // Lazy load - chỉ resolve khi cần dùng
        _llmProviderFactory = llmProviderFactory;
        _pipeline = pipeline;
        _systemPrompts = systemPrompts.Value;
        _logger = logger;
    }

    public static string GetContentBetweenHeader(HeadingBlock? parent, HeadingBlock? child, string source)
    {
        if (parent is null && child is null)
        {
            throw new ArgumentNullException(nameof(parent), nameof(child));
        }
        if (parent is null && child is not null)
        {
            string res = source.Substring(0, child.Span.Start);
            return res;

        }

        if (child is null && parent is not null)
        {
            string res = source.Substring(parent.Span.Start, source.Length - parent.Span.Start);
            return res;
        }



        string contentParent = source.Substring(parent!.Span.Start, parent.Span.Length);
        string contentBetween = source.Substring(parent.Span.End + 1, child!.Span.Start - parent.Span.End - 1);
        if (string.IsNullOrWhiteSpace(contentBetween))
        {
            return string.Empty;
        }

        return $"{contentParent}{contentBetween}";
    }

    public async Task<List<ChunkInfo>> CreateChunkDocument(string source)
    {
        _logger.LogInformation("Starting CreateChunkDocument with source length: {SourceLength}", source.Length);
        var chunks = await CreateChunksAsync(source);
        var tableMerged = chunks
                        .Where(c => c.Type == TypeChunk.Table)
                        .GroupBy(c => c.Title)
                        .Select(g => new ChunkInfo
                        {
                            Title = g.Key,
                            Type = TypeChunk.Table,
                            TittleHirarchy = g.First().TittleHirarchy,
                            TokensCount = g.Sum(x => x.TokensCount),
                            Content = string.Join(
                                $"{Environment.NewLine}{Environment.NewLine}",
                                g.Select(x => x.Content))
                        });

        var others = chunks.Where(c => c.Type != TypeChunk.Table);
        var result = others.Concat(tableMerged).ToList();
        _logger.LogInformation("CreateChunkDocument completed with {ChunkCount} chunks ({TableCount} tables, {OtherCount} others)", 
            result.Count, tableMerged.Count(), others.Count());
        return result;
    }

    private async Task<List<ChunkInfo>> CreateChunksAsync(string source, int headerLevel = 0, string titleChunk = "")
    {
        _logger.LogDebug("CreateChunksAsync called with headerLevel: {HeaderLevel}, title: {Title}", headerLevel, titleChunk);
        var chunks = new List<ChunkInfo>();

        if (string.IsNullOrWhiteSpace(source)) return chunks;

        var tokenResponse = await _tokenCountService.CountAsync(new CountRequest { Text = source, ReturnTokens = false });
        _logger.LogDebug("Token count: {TokenCount}, MaxTokensPerChunk: {MaxTokens}", tokenResponse.TokenCount, _chunkOption.MaxTokensPerChunk);

        if (tokenResponse.TokenCount < _chunkOption.MaxTokensPerChunk)
        {
            var chunkInfo = new ChunkInfo()
            {
                Type = typeChunk,
                TokensCount = tokenResponse.TokenCount,
                Content = source,
                TittleHirarchy = GenerateHirarchyHeader(),
                Title = titleChunk
            };
            chunks.Add(chunkInfo);
            var chunksTable = await CreateChunkTableInSection(source);
            chunks.AddRange(chunksTable);
            return chunks;
        }

        if (headerLevel > _chunkOption.MaxDeepHeader)
        {
            _logger.LogWarning("CreateChunksAsync - headerLevel {HeaderLevel} exceeds MaxDeepHeader {MaxDeepHeader}, switching to regex chunking", 
                headerLevel, _chunkOption.MaxDeepHeader);
            // Không thể chia theo header nữa, chia theo regex
            var regexChunks = await CreateChunkByRegexAsync(source);
            chunks.AddRange(regexChunks);
            _logger.LogDebug("Content preview: {ContentPreview}", source.Length > 50 ? source[..50] : source);
            return chunks;
        }

        var allBlocks = MarkdownServiceHelper.GetAllBlock(source, _pipeline);
        var headers = allBlocks.Where(b => b is HeadingBlock h && h.Level == headerLevel)
                               .Select(b => (HeadingBlock)b)
                               .ToList();

        int countHeader = headers.Count;
        _logger.LogDebug("Found {HeaderCount} headers at level {HeaderLevel}", countHeader, headerLevel);

        if (countHeader == 0)
        {
            _logger.LogDebug("No headers found at level {HeaderLevel}, trying deeper level", headerLevel);
            var subChunks = await CreateChunksAsync(source, headerLevel + 1);
            chunks.AddRange(subChunks);
            return chunks;
        }

        if (countHeader == 1)
        {
            string titleHeader = source.Substring(headers[0].Span.Start, headers[0].Span.Length);
            string headerTopContent = GetContentBetweenHeader(null, headers[0], source);
            UpdateHeaderHirearchy(new KeyValuePair<int, string>(headerLevel, titleHeader));
            var subChunks = await CreateChunksAsync(headerTopContent, headerLevel + 1, titleHeader);
            chunks.AddRange(subChunks);
        }

        for (int i = 0; i < headers.Count - 1; i++)
        {
            if (i == 0)
            {
                ChunkInfo contentHeader = GetContentByIndex(source, headers[0].Span.Start, headers[0].Span.End);
                string contentBelongToFirstHeader = GetContentBetweenHeader(null, headers[1], source);
                UpdateHeaderHirearchy(new KeyValuePair<int, string>(headerLevel, contentHeader.Content));

                var subChunksLoop = await CreateChunksAsync(contentBelongToFirstHeader, headerLevel + 1, contentHeader.Content);
                chunks.AddRange(subChunksLoop);
                continue;
            }

            var currentHeader = headers[i];
            ChunkInfo contentCurrentHeader = GetContentByIndex(source, currentHeader.Span.Start, currentHeader.Span.End);
            var nextHeader = headers[i + 1];
            string contentBetweenHeader = GetContentBetweenHeader(currentHeader, nextHeader, source);
            UpdateHeaderHirearchy(new KeyValuePair<int, string>(headerLevel, contentCurrentHeader.Content));

            var subChunks = await CreateChunksAsync(contentBetweenHeader, headerLevel + 1, contentCurrentHeader.Content);
            chunks.AddRange(subChunks);
        }

        // Xử lý header cuối cùng
        var lastHeader = headers.Last();
        ChunkInfo contentLastHeader = GetContentByIndex(source, lastHeader.Span.Start, lastHeader.Span.End);
        string headerLastContent = GetContentBetweenHeader(lastHeader, null, source);
        UpdateHeaderHirearchy(new KeyValuePair<int, string>(headerLevel, contentLastHeader.Content));

        var lastSubChunks = await CreateChunksAsync(headerLastContent, headerLevel + 1, contentLastHeader.Content);
        chunks.AddRange(lastSubChunks);

        return chunks;
    }

    private ChunkInfo GetContentByIndex(string source, int start, int end)
    {
        if (start < 0 || end > source.Length)
        {
            throw new ArgumentException("Index out of range");
        }
        else
        {
            var content = source.Substring(start, end - start + 1);

            var tokenResponse = _tokenCountService.CountAsync(new CountRequest { Text = content, ReturnTokens = false }).Result;
            var chunkInfo = new ChunkInfo()
            {
                Type = TypeChunk.Text,
                TokensCount = tokenResponse.TokenCount,
                Content = content,
                TittleHirarchy = string.Empty
            };
            return chunkInfo;

        }
    }

    public async Task<List<ChunkInfo>> CreateChunkTableInSection(string source)
    {
        _logger.LogDebug("Starting CreateChunkTableInSection with source length: {SourceLength}", source.Length);
        bool IsTitleTable(Block block)
        {
            if(block is null) { return false; }
            // xử lý nếu như văn bản được xử lý chia page theo stype ------ page x ------
            if(block is ParagraphBlock)
            {
                string content = source.Substring(block.Span.Start, block.Span.Length);
                return !content.Contains("page", StringComparison.OrdinalIgnoreCase) && !content.Contains("trang", StringComparison.OrdinalIgnoreCase);
            }
            return block is ParagraphBlock || block is ListItemBlock || block is HeadingBlock || block is ListBlock;
        }

        bool IsTableBlock(HtmlBlock block)
        {
            if (block is null)
            {
                return false;
            }

            string content = block.Lines.ToString().TrimStart();
            return content.StartsWith("<table");
        }

        string GetContentTableSegments(string source, List<Block> tableSegments)
        {
            var blockTableFirst = tableSegments.First();
            var blockTableLast = tableSegments.Last();
            return source.Substring(blockTableFirst.Span.Start, blockTableLast.Span.End - blockTableFirst.Span.Start + 1);
        }

        List<ChunkInfo> chunks = new List<ChunkInfo>();
        LlmChatCompletionBase llmChatChoice = _llmProviderFactory.GetLlmServiceChoice();

        List<Block> blocks = MarkdownServiceHelper.GetAllBlock(source, _pipeline);

        // List<Block> blockTable = blocks.Where(b => b is Table || (b is HtmlBlock htmlBlock && IsTableBlock(htmlBlock))).ToList();

        int count = blocks.Count;
        List<Block> tableSegments = new List<Block>();
        string titleTable = string.Empty;

        for (int i = 0; i < count; i++)
        {
            Block block = blocks[i];
            bool isTable = block is Table || (block is HtmlBlock htmlBlock && IsTableBlock(htmlBlock));
            if (!isTable)
            {
                continue;
            }
            // string content = source.Substring(block.Span.Start, block.Span.Length);

            if (string.IsNullOrEmpty(titleTable) && i != 0 && IsTitleTable(blocks[i - 1]))
            {
                titleTable = source.Substring(blocks[i - 1].Span.Start, blocks[i - 1].Span.Length);
            }
            if (tableSegments.Count == 0)
            {
                tableSegments.Add(block);
                continue;
            }

            // string table1 = string.Join(@"\n", tableSegments);
            string table1 = GetContentTableSegments(source, tableSegments);
            string table2 = source.Substring(block.Span.Start, block.Span.Length);
            var chatMessageRequests = LlmChatHelper.CreateChatMessageChoice(table1, table2, _systemPrompts.Choice);
            List<string> choices = new List<string>() { "Yes", "No" };
            _logger.LogDebug("Comparing tables for title: {Title}", titleTable);
           
            var choice = await llmChatChoice.ChatChoiceAsync(chatMessageRequests, choices);

            _logger.LogDebug("Table comparison result: {Choice} for title: {Title}", choice, titleTable);
            switch (choice.Trim().ToLowerInvariant())
            {
                case "yes":
                case "y":
                    tableSegments.Add(block);
                    break;
                default:
                    var resToken = await _tokenCountService.CountAsync(new CountRequest { Text = table1, ReturnTokens = false });
                    ChunkInfo chunkInfo = new ChunkInfo()
                    {
                        Type = TypeChunk.Table,
                        TokensCount = resToken.TokenCount,
                        Content = table1,
                        TittleHirarchy = GenerateHirarchyHeader(),
                        Title = titleTable
                    };
                    chunks.Add(chunkInfo);
                    tableSegments.Clear();
                    tableSegments.Add(block);
                    if (i != 0 && IsTitleTable(blocks[i - 1]))
                    {
                        titleTable = source.Substring(blocks[i - 1].Span.Start, blocks[i - 1].Span.Length);
                    }
                    break;
            }

        }

        if (tableSegments.Count > 0)
        {
            var resToken = await _tokenCountService.CountAsync(new CountRequest { Text = GetContentTableSegments(source, tableSegments), ReturnTokens = false });
            ChunkInfo chunkInfo = new ChunkInfo()
            {
                Type = TypeChunk.Table,
                TokensCount = resToken.TokenCount,
                Content = GetContentTableSegments(source, tableSegments),
                TittleHirarchy = GenerateHirarchyHeader(),
                Title = titleTable
            };
            chunks.Add(chunkInfo);
        }
        _logger.LogDebug("CreateChunkTableInSection completed with {ChunkCount} table chunks", chunks.Count);

        return chunks;
    }

    private void UpdateHeaderHirearchy(KeyValuePair<int, string> header)
    {
        if (string.IsNullOrEmpty(header.Value))
        {
            return;
        }
        if (_stackHeaderTitle.Count == 0)
        {
            _stackHeaderTitle.Push(header);
            return;
        }

        // push header
        if (_stackHeaderTitle.Peek().Key < header.Key)
        {
            _stackHeaderTitle.Push(header);
            return;
        }

        // pop header
        while (_stackHeaderTitle.Count > 0 && _stackHeaderTitle.Peek().Key >= header.Key)
        {
            _stackHeaderTitle.Pop();
        }
        _stackHeaderTitle.Push(header);

    }

    private string GenerateHirarchyHeader()
    {
        if (_stackHeaderTitle.Count > 0)
        {
            var pathElements = _stackHeaderTitle.ToArray().Reverse();
            return string.Join(" --> ", pathElements.Select(x => x.Value));
        }
        return string.Empty;
    }

    /// <summary>
    /// Chia chunk theo chiến lược semantic khi không thể chia theo header
    /// Ưu tiên: 1. Bảng → 2. List items → 3. Đoạn văn → 4. Câu
    /// </summary>
    public async Task<List<ChunkInfo>> CreateChunkByRegexAsync(string source)
    {
        _logger.LogInformation("Starting CreateChunkByRegexAsync with source length: {SourceLength}", source.Length);
        var chunks = new List<ChunkInfo>();

        if (string.IsNullOrWhiteSpace(source)) return chunks;

        // Bước 1: Trích xuất tất cả tables trước (vì tables thường là đơn vị ngữ nghĩa hoàn chỉnh)
        _logger.LogDebug("CreateChunkByRegexAsync - Step 1: Extracting tables");
        var tableChunks = await CreateChunkTableInSection(source);
        if (tableChunks.Any())
        {
            _logger.LogDebug("Found {TableChunkCount} table chunks", tableChunks.Count);
            chunks.AddRange(tableChunks);

            // Loại bỏ phần table khỏi source để xử lý phần còn lại
            source = MarkdownServiceHelper.RemoveTablesFromSource(source, tableChunks);

            if (string.IsNullOrWhiteSpace(source)) return chunks;
        }

        // Bước 2: Chia theo các đơn vị ngữ nghĩa từ lớn đến nhỏ
        _logger.LogDebug("CreateChunkByRegexAsync - Step 2: Dividing by semantic units");
        var blocks = MarkdownServiceHelper.GetAllBlock(source, _pipeline);
        var semanticChunks = await DivideBySemanticUnits(source, blocks);
        chunks.AddRange(semanticChunks);
        _logger.LogInformation("CreateChunkByRegexAsync completed with {TotalChunks} chunks ({TableChunks} tables, {SemanticChunks} semantic)", 
            chunks.Count, tableChunks.Count, semanticChunks.Count);

        return chunks;
    }

    /// <summary>
    /// Chia nội dung theo các đơn vị ngữ nghĩa: List → Paragraph → Sentence
    /// </summary>
    private async Task<List<ChunkInfo>> DivideBySemanticUnits(string source, List<Block> blocks)
    {
        var chunks = new List<ChunkInfo>();
        var currentSegment = new List<Block>();
        int currentTokenCount = 0;

        for (int i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            string blockContent = source.Substring(block.Span.Start, block.Span.Length);

            var tokenResponse = await _tokenCountService.CountAsync(
                new CountRequest { Text = blockContent, ReturnTokens = false }
            );

            // Nếu block hiện tại + segment hiện tại vượt quá giới hạn
            if (currentTokenCount + tokenResponse.TokenCount > _chunkOption.MaxTokensPerChunk)
            {
                // Lưu segment hiện tại nếu có nội dung
                if (currentSegment.Any())
                {
                    var chunkContent = MarkdownServiceHelper.ExtractContentFromBlocks(source, currentSegment);
                    chunks.Add(new ChunkInfo
                    {
                        Type = typeChunk,
                        TokensCount = currentTokenCount,
                        Content = chunkContent,
                        TittleHirarchy = GenerateHirarchyHeader()
                    });
                    currentSegment.Clear();
                    currentTokenCount = 0;
                }

                // Nếu block đơn lẻ vẫn quá lớn → chia nhỏ hơn
                if (tokenResponse.TokenCount > _chunkOption.MaxTokensPerChunk)
                {
                    var subChunks = await DivideBlockIntoSmallerChunks(blockContent, block);
                    chunks.AddRange(subChunks);
                    continue;
                }
            }

            // Thêm block vào segment hiện tại
            currentSegment.Add(block);
            currentTokenCount += tokenResponse.TokenCount;
        }

        // Lưu segment cuối cùng
        if (currentSegment.Any())
        {
            var chunkContent = MarkdownServiceHelper.ExtractContentFromBlocks(source, currentSegment);
            chunks.Add(new ChunkInfo
            {
                Type = typeChunk,
                TokensCount = currentTokenCount,
                Content = chunkContent,
                TittleHirarchy = GenerateHirarchyHeader()
            });
        }

        return chunks;
    }

    /// <summary>
    /// Chia block quá lớn thành các chunk nhỏ hơn theo thứ tự ưu tiên:
    /// List items → Paragraphs → Sentences → Fixed size
    /// </summary>
    private async Task<List<ChunkInfo>> DivideBlockIntoSmallerChunks(string content, Block block)
    {
        var chunks = new List<ChunkInfo>();

        // Chiến lược 1: Nếu là ListBlock → chia theo từng ListItem
        if (block is ListBlock listBlock)
        {
            var itemChunks = await DivideListByItems(content, listBlock);
            return itemChunks;
        }

        // Chiến lược 2: Chia theo paragraph (double newline)
        var paragraphs = content.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        if (paragraphs.Length > 1)
        {
            var paragraphChunks = await DivideByParagraphs(paragraphs);
            return paragraphChunks;
        }

        // Chiến lược 3: Chia theo câu (sentence boundary)
        var sentences = MarkdownServiceHelper.SplitIntoSentences(content);
        if (sentences.Count > 1)
        {
            var sentenceChunks = await DivideBySentences(sentences);
            return sentenceChunks;
        }

        // Chiến lược 4 (cuối cùng): Chia cứng theo token count với overlap
        var fixedChunks = await DivideByFixedSizeWithOverlap(content);
        return fixedChunks;
    }

    /// <summary>
    /// Chia ListBlock theo từng ListItem
    /// </summary>
    private async Task<List<ChunkInfo>> DivideListByItems(string content, ListBlock listBlock)
    {
        var chunks = new List<ChunkInfo>();
        var currentItems = new List<string>();
        int currentTokenCount = 0;

        // Parse list items
        var items = MarkdownServiceHelper.ExtractListItems(content);

        foreach (var item in items)
        {
            var tokenResponse = await _tokenCountService.CountAsync(
                new CountRequest { Text = item, ReturnTokens = false }
            );

            if (currentTokenCount + tokenResponse.TokenCount > _chunkOption.MaxTokensPerChunk && currentItems.Any())
            {
                chunks.Add(new ChunkInfo
                {
                    Type = typeChunk,
                    TokensCount = currentTokenCount,
                    Content = string.Join("\n", currentItems),
                    TittleHirarchy = GenerateHirarchyHeader()
                });
                currentItems.Clear();
                currentTokenCount = 0;
            }

            currentItems.Add(item);
            currentTokenCount += tokenResponse.TokenCount;
        }

        if (currentItems.Any())
        {
            chunks.Add(new ChunkInfo
            {
                Type = typeChunk,
                TokensCount = currentTokenCount,
                Content = string.Join("\n", currentItems),
                TittleHirarchy = GenerateHirarchyHeader()
            });
        }

        return chunks;
    }

    /// <summary>
    /// Chia theo đoạn văn
    /// </summary>
    private async Task<List<ChunkInfo>> DivideByParagraphs(string[] paragraphs)
    {
        var chunks = new List<ChunkInfo>();
        var currentParagraphs = new List<string>();
        int currentTokenCount = 0;

        foreach (var paragraph in paragraphs)
        {
            var tokenResponse = await _tokenCountService.CountAsync(
                new CountRequest { Text = paragraph, ReturnTokens = false }
            );

            if (currentTokenCount + tokenResponse.TokenCount > _chunkOption.MaxTokensPerChunk && currentParagraphs.Any())
            {
                chunks.Add(new ChunkInfo
                {
                    Type = typeChunk,
                    TokensCount = currentTokenCount,
                    Content = string.Join("\n\n", currentParagraphs),
                    TittleHirarchy = GenerateHirarchyHeader()
                });
                currentParagraphs.Clear();
                currentTokenCount = 0;
            }

            currentParagraphs.Add(paragraph);
            currentTokenCount += tokenResponse.TokenCount;
        }

        if (currentParagraphs.Any())
        {
            chunks.Add(new ChunkInfo
            {
                Type = typeChunk,
                TokensCount = currentTokenCount,
                Content = string.Join("\n\n", currentParagraphs),
                TittleHirarchy = GenerateHirarchyHeader()
            });
        }

        return chunks;
    }

    /// <summary>
    /// Chia theo câu với sentence boundary detection
    /// </summary>
    private async Task<List<ChunkInfo>> DivideBySentences(List<string> sentences)
    {
        var chunks = new List<ChunkInfo>();
        var currentSentences = new List<string>();
        int currentTokenCount = 0;

        foreach (var sentence in sentences)
        {
            var tokenResponse = await _tokenCountService.CountAsync(
                new CountRequest { Text = sentence, ReturnTokens = false }
            );

            if (currentTokenCount + tokenResponse.TokenCount > _chunkOption.MaxTokensPerChunk && currentSentences.Any())
            {
                chunks.Add(new ChunkInfo
                {
                    Type = typeChunk,
                    TokensCount = currentTokenCount,
                    Content = string.Join(" ", currentSentences),
                    TittleHirarchy = GenerateHirarchyHeader()
                });
                currentSentences.Clear();
                currentTokenCount = 0;
            }

            currentSentences.Add(sentence);
            currentTokenCount += tokenResponse.TokenCount;
        }

        if (currentSentences.Any())
        {
            chunks.Add(new ChunkInfo
            {
                Type = typeChunk,
                TokensCount = currentTokenCount,
                Content = string.Join(" ", currentSentences),
                TittleHirarchy = GenerateHirarchyHeader()
            });
        }

        return chunks;
    }

    /// <summary>
    /// Chia cứng theo token với overlap để giữ context
    /// </summary>
    private async Task<List<ChunkInfo>> DivideByFixedSizeWithOverlap(string content)
    {
        var chunks = new List<ChunkInfo>();
        int overlapTokens = _chunkOption.MaxTokensPerChunk / 10; // 10% overlap
        int targetTokens = _chunkOption.MaxTokensPerChunk - overlapTokens;

        var words = content.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var currentChunk = new List<string>();
        var overlapBuffer = new List<string>();
        int currentTokenCount = 0;

        foreach (var word in words)
        {
            currentChunk.Add(word);

            // Ước tính token (thường 1 word ≈ 1.3 tokens cho tiếng Anh, điều chỉnh nếu cần)
            currentTokenCount += 2;

            if (currentTokenCount >= targetTokens)
            {
                var chunkContent = string.Join(" ", currentChunk);
                var actualTokens = await _tokenCountService.CountAsync(
                    new CountRequest { Text = chunkContent, ReturnTokens = false }
                );

                chunks.Add(new ChunkInfo
                {
                    Type = typeChunk,
                    TokensCount = actualTokens.TokenCount,
                    Content = chunkContent,
                    TittleHirarchy = GenerateHirarchyHeader()
                });

                // Giữ lại phần overlap
                int overlapSize = Math.Min(currentChunk.Count / 10, currentChunk.Count);
                overlapBuffer = currentChunk.Skip(currentChunk.Count - overlapSize).ToList();
                currentChunk = new List<string>(overlapBuffer);
                currentTokenCount = overlapSize * 2;
            }
        }

        if (currentChunk.Any())
        {
            var chunkContent = string.Join(" ", currentChunk);
            var actualTokens = await _tokenCountService.CountAsync(
                new CountRequest { Text = chunkContent, ReturnTokens = false }
            );

            chunks.Add(new ChunkInfo
            {
                Type = typeChunk,
                TokensCount = actualTokens.TokenCount,
                Content = chunkContent,
                TittleHirarchy = GenerateHirarchyHeader()
            });
        }

        return chunks;
    }


}
