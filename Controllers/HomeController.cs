using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ExamCreator.Models;
using System.Linq.Expressions;
using System.Xml;
using Microsoft.SyndicationFeed.Rss;
using Microsoft.SyndicationFeed;
using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;

namespace ExamCreator.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            using (var db = new Context())
            {
                //Create User
                //db.Add(new User { Username = "admin", Password = "password" });
                //db.SaveChanges();
                //Console.WriteLine("New user inserted");
            }

            _logger = logger;
        }

        public IActionResult Index()
        {
            using (var db = new Context())
            {
               // db.Add(new Exam { Title = "dfgfdg", Content = "İçerik burası" });
               //db.SaveChanges();
              
                List<Exam> exams = db.Exams.ToList();

                return View(exams);
            }
            
        }

        public IActionResult CreateExam()
        {

            List<ArticleViewModel> articles = GetArticleContent();

            return View(articles);
        }

        [HttpPost]
        public IActionResult CreateExam(string data)
        {
            var examObject = JObject.Parse(data);
            var questionArray = JArray.Parse(examObject["question"].ToString());

            Exam exam = new Exam()
            {
                Title = examObject["title"].ToString(),
                Content = examObject["content"].ToString()
            };

            using (var db = new Context())
            {
                db.Add(exam);
                db.SaveChanges();
                exam = db.Exams.LastOrDefault();
            }

            for (int i = 0; i < questionArray.Count(); i++)
            {
                var questionObject = JObject.Parse(questionArray[i].ToString());
                Question question = new Question()
                {
                    QuestionText = questionObject["q"].ToString(),
                    AnswerA = questionObject["a"].ToString(),
                    AnswerB = questionObject["b"].ToString(),
                    AnswerC = questionObject["c"].ToString(),
                    AnswerD = questionObject["d"].ToString(),
                    CorrectAnswer = questionObject["s"].ToString(),
                    ExamId = exam.ExamId 
                };

                using (var db = new Context())
                {
                    db.Add(question);
                    db.SaveChanges();
                }

            }

            return RedirectToAction("Index", "Home");
        }

        public IActionResult SolveExam(int id)
        {
            using (var db = new Context())
            {
                ExamViewModel exam = new ExamViewModel()
                {
                    Title = db.Exams.Where(x=>x.ExamId==id).Select(x=>x.Title).SingleOrDefault(),
                    Content = db.Exams.Where(x => x.ExamId == id).Select(x => x.Content).SingleOrDefault(),
                    QuestionList = db.Questions.Where(x => x.ExamId == id).ToList()
                };

                return View(exam);
            }

        }

        public IActionResult DeleteExam(int id)
        {
            using (var db = new Context())
            {
                var exam = db.Exams.Where(x => x.ExamId == id).SingleOrDefault();
                var questionList = db.Questions.Where(x => x.ExamId == id).ToList();
                db.Remove(exam);
                db.RemoveRange(questionList);
                db.SaveChanges();

                return RedirectToAction("Index");
            }

        }

        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(User user)
        {


            Expression<Func<User, bool>> filter = x => x.Username == user.Username && x.Password == user.Password;
            using (var db = new Context())
            {
                User currentUser = db.Users.Where(filter).SingleOrDefault();
                if (currentUser != null)
                {

                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    TempData["warning"] = "Kullanıcı adı ya da şifre bilgileri hatalı.";
                }
            }

            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }


        public static async Task<List<ISyndicationContent>> RssFeedReader()
        {
            string feedUri = "https://www.wired.com/feed/rss";
            List<ISyndicationContent> contentList = new List<ISyndicationContent>();
            using (var xmlReader = XmlReader.Create(feedUri, new XmlReaderSettings() { Async = true }))
            {
                var feedReader = new RssFeedReader(xmlReader);

                while (await feedReader.Read())
                {

                    ISyndicationContent content = await feedReader.ReadContent();

                    if(content.Name=="item")
                    contentList.Add(content);
                }
            }

            return contentList;
        }

        public List<ArticleViewModel> GetArticleContent() {

            List<ArticleViewModel> articleList = new List<ArticleViewModel>();
            var contentList = RssFeedReader().Result;

            contentList.Take(5).ToList().ForEach(content =>
            {
                String articleUrl = content.Fields.Where(y => y.Name == "link").Select(z => z.Value).SingleOrDefault();

                HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
                WebClient client = new WebClient();
                doc.LoadHtml(client.DownloadString(articleUrl));
                var articleBody = doc.DocumentNode.SelectNodes("//div[contains(@class,'article__body')]");
                if (articleBody!=null) { 
                StringBuilder articleText = new StringBuilder();
                for (int i = 0; i < articleBody.Count - 1; i++)
                {
                    var innerNodes = articleBody[i];

                    foreach (var node in innerNodes.SelectNodes("*"))
                    {
                        if (node.Name == "p")
                        {
                            articleText.Append(node.InnerText);
                        }
                    }


                }
                ArticleViewModel article = new ArticleViewModel
                {
                    Title = content.Fields.Where(y => y.Name == "title").Select(z => z.Value).SingleOrDefault(),
                    Content = articleText.ToString()
                };
                articleList.Add(article);
                }
            });


            return articleList.ToList();

        }



    }
}
