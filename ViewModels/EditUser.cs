using System;
using System.ComponentModel.DataAnnotations;


namespace Itsomax.Module.UserManagement.ViewModels
{
    public class EditUser
    {
        public EditUser()
        {
            UpdatedOn = DateTimeOffset.Now;
        }
        [Required]
        public long Id;
        [Required]
        [EmailAddress]
        public string Email { get; set; }
        //public string Password { get; set; }
        public DateTimeOffset UpdatedOn { get; set; }
    }
}