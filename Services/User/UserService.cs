using Exatech_Indotel_API.Entities;
using Exatech_Indotel_API.Models.User;
using Exatech_Indotel_API.Repositories.UserRepository;
using Exatech_Indotel_API.Services.Email;
using Exatech_Indotel_API.Utilities;


namespace Exatech_Indotel_API.Services.User
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IEmailSenderService _emailSender;
        public UserService(IUserRepository userRepository, IEmailSenderService emailSender)
        {
            _userRepository = userRepository;
            _emailSender = emailSender;
        }

        public async Task CreateUser(CreateUserRequest user)
        {
            string originalPassword = user.Password;

            Entities.User newUser = new Entities.User
            {
                Email = user.Email,
                Password = user.Password.Sha256Hash(),
                Name = user.Name,
                Phone = user.Phone,
                RoleId = user.RoleId
            };

            await _userRepository.CreateUser(newUser);

            await _emailSender.SendEmail(newUser, EventType.UserCreation, (template, user) => 
                template.Template.Replace("$$Email$$",user.Email)
                .Replace("$$Password$$",originalPassword)
                .Replace("$$Name$$",user.Name)
                .Replace("$$RoleId$$", newUser?.Role?.RoleName), [newUser.Email]);
        }
    }
}
