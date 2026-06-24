using Microsoft.Extensions.AI;
using TokensAndCredits.Web.Services.Models;

namespace TokensAndCredits.Web.Services.Chat;

/// <summary>Creates a unified <see cref="IChatClient"/> for an Azure or local model.</summary>
public interface IChatClientFactory
{
    Task<IChatClient> CreateAsync(ModelDescriptor model, CancellationToken ct);
}
