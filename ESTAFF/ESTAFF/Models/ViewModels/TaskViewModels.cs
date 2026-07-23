using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ESTAFF.Models.Data;

namespace ESTAFF.Models.ViewModels
{
    public class AssignTaskViewModel
    {
        [Required(ErrorMessage = "Title is required")]
        [StringLength(256)]
        [Display(Name = "Task Title")]
        public string Title { get; set; }

        [Display(Name = "Description")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Please assign to an employee")]
        [Display(Name = "Assign To")]
        public string AssignedToUserId { get; set; }

        [Required(ErrorMessage = "Due date is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Due Date")]
        public DateTime DueDate { get; set; } = DateTime.Today.AddDays(7);

        [Display(Name = "Priority")]
        public TaskPriority? Priority { get; set; }

        // Dropdown List of Employees
        public List<EmployeeSelectItem> Employees { get; set; } 
            = new List<EmployeeSelectItem>();
    }

    public class EditTaskViewModel
    {
        public int TaskId { get; set; }

        [Required(ErrorMessage = "Title is required")]
        [StringLength(256)]
        [Display(Name = "Task Title")]
        public string Title { get; set; }

        [Display(Name = "Description")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Please assign to an employee")]
        [Display(Name = "Assign To")]
        public string AssignedToUserId { get; set; }

        [Required(ErrorMessage = "Due date is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Due Date")]
        public DateTime DueDate { get; set; }

        [Display(Name = "Priority")]
        public TaskPriority? Priority { get; set; }

        [Display(Name = "Status")]
        public TaskStatus Status { get; set; }

        // Dropdown List
        public List<EmployeeSelectItem> Employees { get; set; }
            = new List<EmployeeSelectItem>();
    }

    public class TaskListItemViewModel
    {
        public int TaskId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int? COFId { get; set; }
        public TaskStatus Status { get; set; }
        public TaskPriority? Priority { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public string AssignedToUserId { get; set; }
        public string AssignedToName { get; set; }
        public string AssignedToEmpID { get; set; }
        public string CreatedByName { get; set; }
        public bool IsOverdue => Status != TaskStatus.Complete
            && DueDate.Date < DateTime.Today;
    }

    public class TaskHistoryItemViewModel
    {
        public int HistoryId { get; set; }
        public int TaskId { get; set; }
        public string TaskTitle { get; set; }
        public string Action { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public string ChangedByName { get; set; }
        public DateTime ChangedDate { get; set; }
    }

    public class EmployeeSelectItem
    {
        public string UserId { get; set; }
        public string FullName { get; set; }
        public string EmpID { get; set; }
        public string Display => $"{FullName} ({EmpID})";
    }


    // Employee Side
    public class CreateTaskViewModel
    {
        [Required(ErrorMessage = "Title is required")]
        [StringLength(256)]
        [Display(Name = "Task Title")]
        public string Title { get; set; }

        [Display(Name = "Description")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Due date is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Due Date")]
        public DateTime DueDate { get; set; } = DateTime.Today.AddDays(1);

        [Display(Name = "Certificate Of Fitness (COF)")]
        public int? COFId { get; set; }

        [Display(Name = "Priority")]
        public TaskPriority? Priority { get; set; }
    }

    public class UpdateTaskStatusViewModel
    {
        public int TaskId { get; set; }
        public TaskStatus Status { get; set; }
    }
}