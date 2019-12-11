using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;

namespace BoutinFlegel.Authentication.Proto
{
	[Authorize(Policy = "protectedScope")]
	public class GreetService : Greeter.GreeterBase
	{
		public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
			=> Task.FromResult(new HelloReply
			{
				Message = $"Hello {request.Name}"
			});
	}
}
