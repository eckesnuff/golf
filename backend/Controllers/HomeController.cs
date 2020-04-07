using Microsoft.AspNetCore.Mvc;
namespace backend
{
    public class Home : Controller
    {
        public IActionResult Index(){
            return View();
        }
    }
}