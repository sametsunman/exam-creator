using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace ExamCreator.Models
{
    public class Exam
    {
        [Key]
        public int ExamId { get; set; }
        public string Paragraph { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
