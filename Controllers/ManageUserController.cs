using Microsoft.AspNetCore.Mvc;
using Itsomax.Module.UserManagement.Interfaces;
using Itsomax.Module.UserManagement.ViewModels;
using System.Threading.Tasks;

namespace Itsomax.Module.UserManagement.Controllers
{
    public class ManageUserController : Controller
    {
        private readonly IManageUser _manageUser;

        public ManageUserController(IManageUser manageUser)
        {
            _manageUser=manageUser;
        }

        public IActionResult CreateUser()
        {
            return View();
        }

        /*
        public Task<IActionResult> CreateUser(CreateUserViewModel model, params string [] roles)
        {
            if (ModelState.IsValid)
            {
               // _manageUser.CreateUser();
            }
            return View();
        }
        */
    }
}