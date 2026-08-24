using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace HRMS.Infrastructure.Data;

/// <summary>
/// EF Core caches the compiled model per DbContext CLR type. ApplicationDbContext
/// builds a different model depending on whether <c>Security:EncryptionKey</c> is
/// configured (PII value converters present or absent), so the default cache key
/// can hand a converter-free model to a context that expects encryption — silently
/// storing Aadhaar / PAN / bank details as plaintext at rest.
///
/// This factory folds the encryption state into the cache key so each variant gets
/// its own compiled model.
/// </summary>
public sealed class EncryptionAwareModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
        => context is ApplicationDbContext app
            ? (context.GetType(), app.PiiEncryptionEnabled, designTime)
            : (object)(context.GetType(), designTime);
}
