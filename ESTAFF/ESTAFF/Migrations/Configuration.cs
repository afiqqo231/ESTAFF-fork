using System;
using System.Data.Entity.Migrations;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using ESTAFF.Models.Data;

namespace ESTAFF.Migrations
{
    internal sealed class Configuration : DbMigrationsConfiguration<ApplicationDbContext>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = true;
            AutomaticMigrationDataLossAllowed = false;
        }

        protected override void Seed(ApplicationDbContext context)
        {
            var userManager = new UserManager<ApplicationUser>(
                new UserStore<ApplicationUser>(context)
            );

            string adminEmail = "admin";
            string adminPassword = "Admin123";

            if (userManager.FindByEmail(adminEmail) == null)
            {
                var manager = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "System Admin",
                    IsAdmin = true,
                    IsActive = true,
                    EmpNumber = null,
                    CreatedDate = DateTime.Now,
                    LastModifiedDate = DateTime.Now
                };

                userManager.Create(manager, adminPassword);
            }
        }
    }
}