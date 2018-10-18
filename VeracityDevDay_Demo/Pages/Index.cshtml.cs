using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using System.Threading.Tasks;
using Veracity.Services.Api;

namespace VeracityDevDay_Demo.Pages
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly IApiClient _client;

        public IndexModel(IApiClient client)
        {
            _client = client;
        }
        public async Task OnGet()
        {
            var myProfile = await _client.My.Info();
            MyData = JsonConvert.SerializeObject(myProfile, Formatting.Indented);
        }

        public string MyData { get; set; }
    }
}
