namespace ESTAFF.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialCreate : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Reports",
                c => new
                    {
                        ReportId = c.Int(nullable: false, identity: true),
                        UserId = c.String(nullable: false, maxLength: 128),
                        ReportType = c.Int(nullable: false),
                        PeriodStart = c.DateTime(nullable: false),
                        PeriodEnd = c.DateTime(nullable: false),
                        Content = c.String(),
                        Status = c.Int(nullable: false),
                        SubmittedDate = c.DateTime(),
                        ApprovedDate = c.DateTime(),
                        RejectionReason = c.String(),
                        CreatedDate = c.DateTime(nullable: false),
                        LastModifiedDate = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.ReportId)
                .ForeignKey("dbo.AspNetUsers", t => t.UserId)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.AspNetUsers",
                c => new
                    {
                        Id = c.String(nullable: false, maxLength: 128),
                        FullName = c.String(),
                        EmpNumber = c.String(),
                        IsAdmin = c.Boolean(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                        HireDate = c.DateTime(),
                        CreatedDate = c.DateTime(nullable: false),
                        LastModifiedDate = c.DateTime(nullable: false),
                        Email = c.String(maxLength: 256),
                        EmailConfirmed = c.Boolean(nullable: false),
                        PasswordHash = c.String(),
                        SecurityStamp = c.String(),
                        PhoneNumber = c.String(),
                        PhoneNumberConfirmed = c.Boolean(nullable: false),
                        TwoFactorEnabled = c.Boolean(nullable: false),
                        LockoutEndDateUtc = c.DateTime(),
                        LockoutEnabled = c.Boolean(nullable: false),
                        AccessFailedCount = c.Int(nullable: false),
                        UserName = c.String(nullable: false, maxLength: 256),
                    })
                .PrimaryKey(t => t.Id)
                .Index(t => t.UserName, unique: true, name: "UserNameIndex");
            
            CreateTable(
                "dbo.AspNetUserClaims",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        UserId = c.String(nullable: false, maxLength: 128),
                        ClaimType = c.String(),
                        ClaimValue = c.String(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.AspNetUsers", t => t.UserId, cascadeDelete: true)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.AspNetUserLogins",
                c => new
                    {
                        LoginProvider = c.String(nullable: false, maxLength: 128),
                        ProviderKey = c.String(nullable: false, maxLength: 128),
                        UserId = c.String(nullable: false, maxLength: 128),
                    })
                .PrimaryKey(t => new { t.LoginProvider, t.ProviderKey, t.UserId })
                .ForeignKey("dbo.AspNetUsers", t => t.UserId, cascadeDelete: true)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.AspNetUserRoles",
                c => new
                    {
                        UserId = c.String(nullable: false, maxLength: 128),
                        RoleId = c.String(nullable: false, maxLength: 128),
                    })
                .PrimaryKey(t => new { t.UserId, t.RoleId })
                .ForeignKey("dbo.AspNetUsers", t => t.UserId, cascadeDelete: true)
                .ForeignKey("dbo.AspNetRoles", t => t.RoleId, cascadeDelete: true)
                .Index(t => t.UserId)
                .Index(t => t.RoleId);
            
            CreateTable(
                "dbo.AspNetRoles",
                c => new
                    {
                        Id = c.String(nullable: false, maxLength: 128),
                        Name = c.String(nullable: false, maxLength: 256),
                    })
                .PrimaryKey(t => t.Id)
                .Index(t => t.Name, unique: true, name: "RoleNameIndex");
            
            CreateTable(
                "dbo.TaskHistories",
                c => new
                    {
                        HistoryId = c.Int(nullable: false, identity: true),
                        TaskId = c.Int(nullable: false),
                        Action = c.String(nullable: false, maxLength: 50),
                        OldValue = c.String(),
                        NewValue = c.String(),
                        ChangedByUserId = c.String(nullable: false, maxLength: 128),
                        ChangedDate = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.HistoryId)
                .ForeignKey("dbo.AspNetUsers", t => t.ChangedByUserId)
                .ForeignKey("dbo.TaskItems", t => t.TaskId, cascadeDelete: true)
                .Index(t => t.TaskId)
                .Index(t => t.ChangedByUserId);
            
            CreateTable(
                "dbo.TaskItems",
                c => new
                    {
                        TaskId = c.Int(nullable: false, identity: true),
                        Title = c.String(nullable: false, maxLength: 256),
                        Description = c.String(),
                        Status = c.Int(nullable: false),
                        Priority = c.Int(),
                        DueDate = c.DateTime(nullable: false),
                        AssignedToUserId = c.String(nullable: false, maxLength: 128),
                        CreatedByUserId = c.String(nullable: false, maxLength: 128),
                        AssignedDate = c.DateTime(nullable: false),
                        CompletedDate = c.DateTime(),
                        CreatedDate = c.DateTime(nullable: false),
                        LastModifiedDate = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.TaskId)
                .ForeignKey("dbo.AspNetUsers", t => t.AssignedToUserId)
                .ForeignKey("dbo.AspNetUsers", t => t.CreatedByUserId)
                .Index(t => t.AssignedToUserId)
                .Index(t => t.CreatedByUserId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.TaskHistories", "TaskId", "dbo.TaskItems");
            DropForeignKey("dbo.TaskItems", "CreatedByUserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.TaskItems", "AssignedToUserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.TaskHistories", "ChangedByUserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.AspNetUserRoles", "RoleId", "dbo.AspNetRoles");
            DropForeignKey("dbo.Reports", "UserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.AspNetUserRoles", "UserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.AspNetUserLogins", "UserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.AspNetUserClaims", "UserId", "dbo.AspNetUsers");
            DropIndex("dbo.TaskItems", new[] { "CreatedByUserId" });
            DropIndex("dbo.TaskItems", new[] { "AssignedToUserId" });
            DropIndex("dbo.TaskHistories", new[] { "ChangedByUserId" });
            DropIndex("dbo.TaskHistories", new[] { "TaskId" });
            DropIndex("dbo.AspNetRoles", "RoleNameIndex");
            DropIndex("dbo.AspNetUserRoles", new[] { "RoleId" });
            DropIndex("dbo.AspNetUserRoles", new[] { "UserId" });
            DropIndex("dbo.AspNetUserLogins", new[] { "UserId" });
            DropIndex("dbo.AspNetUserClaims", new[] { "UserId" });
            DropIndex("dbo.AspNetUsers", "UserNameIndex");
            DropIndex("dbo.Reports", new[] { "UserId" });
            DropTable("dbo.TaskItems");
            DropTable("dbo.TaskHistories");
            DropTable("dbo.AspNetRoles");
            DropTable("dbo.AspNetUserRoles");
            DropTable("dbo.AspNetUserLogins");
            DropTable("dbo.AspNetUserClaims");
            DropTable("dbo.AspNetUsers");
            DropTable("dbo.Reports");
        }
    }
}
