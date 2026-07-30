namespace ESTAFF.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class addTaskClassificationAndTaskList : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "ESTAFF.TaskClassifications",
                c => new
                    {
                        TaskClassificationId = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false, maxLength: 100),
                    })
                .PrimaryKey(t => t.TaskClassificationId);
            
            CreateTable(
                "ESTAFF.TaskLists",
                c => new
                    {
                        TaskListId = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false, maxLength: 100),
                        Description = c.String(maxLength: 100),
                        TaskClassificationId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.TaskListId)
                .ForeignKey("ESTAFF.TaskClassifications", t => t.TaskClassificationId)
                .Index(t => t.TaskClassificationId);
            
            AddColumn("ESTAFF.TaskItems", "TaskListId", c => c.Int(nullable: false));
            CreateIndex("ESTAFF.TaskItems", "TaskListId");
            AddForeignKey("ESTAFF.TaskItems", "TaskListId", "ESTAFF.TaskLists", "TaskListId");
        }
        
        public override void Down()
        {
            DropForeignKey("ESTAFF.TaskItems", "TaskListId", "ESTAFF.TaskLists");
            DropForeignKey("ESTAFF.TaskLists", "TaskClassificationId", "ESTAFF.TaskClassifications");
            DropIndex("ESTAFF.TaskItems", new[] { "TaskListId" });
            DropIndex("ESTAFF.TaskLists", new[] { "TaskClassificationId" });
            DropColumn("ESTAFF.TaskItems", "TaskListId");
            DropTable("ESTAFF.TaskLists");
            DropTable("ESTAFF.TaskClassifications");
        }
    }
}
