using Itsomax.Module.UserManagement.Interfaces;
using Itsomax.Module.Core.Extensions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Itsomax.Module.Core.Models;
using System;
using Itsomax.Module.UserManagement.ViewModels;

namespace Itsomax.Module.UserManagement.Services
{
    public class ManageUser : IManageUser
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<Role> _roleManager;
        private readonly SignInManager<User> _signIn;

        public ManageUser(UserManager<User> userManager,RoleManager<Role> roleManager,SignInManager<User> signIn)
        {
            _roleManager = roleManager;
            _userManager = userManager;
            _signIn = signIn;

        }

        public async Task<SucceededTask> CreateUser(string userName, string email, string password, params string[] roles)
        {
            var user = new User()
            {
                Email = email,
                UserName = userName
            };
            var resUser = await _userManager.CreateAsync(user, password);
            if(resUser.Succeeded)
            {
                await _userManager.SetLockoutEnabledAsync(user, true);
                var resRole = await _userManager.AddToRolesAsync(user, roles);
                if(resRole.Succeeded)
                {
                    return SucceededTask.Success;
                }
                else
                {
                    await DeleteUser(user.Id);
                    //TODO:add text based on database and languaje
                    return SucceededTask.Failed("ErrorUserCreate");
                }
            }
            else
            {
                return SucceededTask.Failed("ErrorUserCreate");
            }

        }
        public async Task<SucceededTask> DeleteUser(long Id)
        {
            var user = await _userManager.FindByIdAsync(Id.ToString());
            var resDelUSer = await _userManager.DeleteAsync(user);
            if(resDelUSer.Succeeded)
            {
                return SucceededTask.Success;
            }
            else
            {
                return SucceededTask.Failed("ErrorUserDelete");
            }
        }
        public async Task<SucceededTask> EditUSer(User user)
        {
            var EditUser = _userManager.FindByIdAsync(user.Id.ToString()).Result;

            EditUser.Email = user.Email;
            EditUser.NormalizedEmail = user.NormalizedEmail;

            var res = await _userManager.UpdateAsync(EditUser);
            if(res.Succeeded)
            {
                return SucceededTask.Success;
            }
            else
            {
                return SucceededTask.Failed("ErrorUserEdit");
            }
        }
        public async Task<SucceededTask> LoginUser(LoginUserViewModel model)
        {
            var user = _userManager.FindByNameAsync(model.UserName).Result;
            if(user!=null)
            {
                var res = await _signIn.PasswordSignInAsync(user,model.Password,model.RememberMe,true);
                if(res.Succeeded)
                {
                    return SucceededTask.Success;
                }
                else
                {
                    return SucceededTask.Failed("ErrorLogin");
                }
            }
            else
            {
                return SucceededTask.Failed("WrongUserPassword");
            }
            
        }
    }
}