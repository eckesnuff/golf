using Microsoft.AspNetCore.Mvc;
namespace backend
{
    public class Home : Controller
    {
        public IActionResult Index(string q = null){
            if(q!=null){
                return View("test");
            }
            return View();
        }
    }
}