namespace ESTAFF.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class newInitialMigration : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "ESTAFF.ReportApprovals",
                c => new
                    {
                        ApprovalId = c.Int(nullable: false, identity: true),
                        ReportId = c.Int(nullable: false),
                        ManagerId = c.String(nullable: false, maxLength: 128),
                        ApprovalStatus = c.Int(nullable: false),
                        Comments = c.String(),
                        ActionDate = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.ApprovalId)
                .ForeignKey("CLIP.AspNetUsers", t => t.ManagerId, cascadeDelete: true)
                .ForeignKey("ESTAFF.Reports", t => t.ReportId, cascadeDelete: true)
                .Index(t => t.ReportId)
                .Index(t => t.ManagerId);
            
            CreateTable(
                "ESTAFF.Reports",
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
                .ForeignKey("CLIP.AspNetUsers", t => t.UserId)
                .Index(t => t.UserId);
            
            CreateTable(
                "ESTAFF.TaskHistories",
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
                .ForeignKey("CLIP.AspNetUsers", t => t.ChangedByUserId)
                .ForeignKey("ESTAFF.TaskItems", t => t.TaskId, cascadeDelete: true)
                .Index(t => t.TaskId)
                .Index(t => t.ChangedByUserId);
            
            CreateTable(
                "ESTAFF.TaskItems",
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
                .ForeignKey("CLIP.AspNetUsers", t => t.AssignedToUserId)
                .ForeignKey("CLIP.AspNetUsers", t => t.CreatedByUserId)
                .Index(t => t.AssignedToUserId)
                .Index(t => t.CreatedByUserId);
            
            CreateTable(
                "ESTAFF.Staffs",
                c => new
                    {
                        StaffId = c.String(nullable: false, maxLength: 128),
                        UserId = c.String(nullable: false, maxLength: 128),
                        ManagerId = c.String(nullable: false, maxLength: 128),
                        Department = c.String(maxLength: 100),
                        HireDate = c.DateTime(nullable: false),
                        CreatedDate = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.StaffId)
                .ForeignKey("CLIP.AspNetUsers", t => t.ManagerId)
                .ForeignKey("CLIP.AspNetUsers", t => t.UserId)
                .Index(t => t.UserId)
                .Index(t => t.ManagerId);
            
            AddColumn("CLIP.AspNetUsers", "IsAdmin", c => c.Boolean(nullable: false, defaultValue: false));
            AddColumn("CLIP.AspNetUsers", "ProfilePicturePath", c => c.String());
            AddColumn("CLIP.AspNetUsers", "HireDate", c => c.DateTime());
            AddColumn("CLIP.AspNetUsers", "CreatedDate", c => c.DateTime());
            AddColumn("CLIP.AspNetUsers", "LastModifiedDate", c => c.DateTime());
            
            Sql("UPDATE CLIP.AspNetUsers SET CreatedDate = GETDATE() WHERE CreatedDate IS NULL");
            Sql("UPDATE CLIP.AspNetUsers SET LastModifiedDate = GETDATE() WHERE LastModifiedDate IS NULL");
            
        }
        
        public override void Down()
        {
            DropForeignKey("ESTAFF.Staffs", "UserId", "CLIP.AspNetUsers");
            DropForeignKey("ESTAFF.Staffs", "ManagerId", "CLIP.AspNetUsers");
            DropForeignKey("ESTAFF.TaskHistories", "TaskId", "ESTAFF.TaskItems");
            DropForeignKey("ESTAFF.TaskItems", "CreatedByUserId", "CLIP.AspNetUsers");
            DropForeignKey("ESTAFF.TaskItems", "AssignedToUserId", "CLIP.AspNetUsers");
            DropForeignKey("ESTAFF.TaskHistories", "ChangedByUserId", "CLIP.AspNetUsers");
            DropForeignKey("ESTAFF.ReportApprovals", "ReportId", "ESTAFF.Reports");
            DropForeignKey("ESTAFF.Reports", "UserId", "CLIP.AspNetUsers");
            DropForeignKey("ESTAFF.ReportApprovals", "ManagerId", "CLIP.AspNetUsers");
            DropIndex("ESTAFF.Staffs", new[] { "ManagerId" });
            DropIndex("ESTAFF.Staffs", new[] { "UserId" });
            DropIndex("ESTAFF.TaskItems", new[] { "CreatedByUserId" });
            DropIndex("ESTAFF.TaskItems", new[] { "AssignedToUserId" });
            DropIndex("ESTAFF.TaskHistories", new[] { "ChangedByUserId" });
            DropIndex("ESTAFF.TaskHistories", new[] { "TaskId" });
            DropIndex("ESTAFF.Reports", new[] { "UserId" });
            DropIndex("ESTAFF.ReportApprovals", new[] { "ManagerId" });
            DropIndex("ESTAFF.ReportApprovals", new[] { "ReportId" });
            DropTable("ESTAFF.Staffs");
            DropTable("ESTAFF.TaskItems");
            DropTable("ESTAFF.TaskHistories");
            DropTable("ESTAFF.Reports");
            DropTable("ESTAFF.ReportApprovals");
            
            DropColumn("CLIP.AspNetUsers", "LastModifiedDate");
            DropColumn("CLIP.AspNetUsers", "CreatedDate");
            DropColumn("CLIP.AspNetUsers", "HireDate");
            DropColumn("CLIP.AspNetUsers", "ProfilePicturePath");
            DropColumn("CLIP.AspNetUsers", "IsAdmin");
        }
    }
}
