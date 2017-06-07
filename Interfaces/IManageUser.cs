using System.Threading.Tasks;
using Itsomax.Module.Core.Extensions;
using Itsomax.Module.Core.Models;
using Itsomax.Module.UserManagement.ViewModels;

namespace Itsomax.Module.UserManagement.Interfaces
{
    public interface IManageUser
    {
        Task<SucceededTask> CreateUser(string userName,string email, string password,params string[] roles);
        Task<SucceededTask> DeleteUser(long Id);
        Task<SucceededTask> EditUSer(User user);
        Task<SucceededTask> LoginUser(LoginUserViewModel model);
    }
}