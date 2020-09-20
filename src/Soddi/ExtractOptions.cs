using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using MediatR;

namespace Soddi
{
    [Verb("extract")]
    public class ExtractOptions : IRequest<int>
    {
    }

    public class ExtractHandler : IRequestHandler<ExtractOptions, int>
    {
        public async Task<int> Handle(ExtractOptions request, CancellationToken cancellationToken)
        {
            return await Task.FromResult(0);
        }
    }
}
