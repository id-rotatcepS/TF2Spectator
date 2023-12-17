using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TF2WebInterface.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(ILogger<IndexModel> logger)
        {
            _logger = logger;
        }

        public string OutputString { get; private set; }

        public void OnGet()
        {
            TF2FrameworkInterface.TF2Instance x = TF2FrameworkInterface.TF2Instance.CreateCommunications();
            x.SendCommand(new TF2FrameworkInterface.StringCommand("echo web"), (s) => OutputString = s);
        }
    }
}
