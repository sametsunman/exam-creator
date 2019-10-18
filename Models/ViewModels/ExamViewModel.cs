using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExamCreator.Models
{
    public class ExamViewModel
    {
        public string Title { get; set; }
        public string Content { get; set; }
        public List<Question> QuestionList { get; set; }
    }
}
