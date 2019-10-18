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
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace ExamCreator.Controllers
{
    [Authorize]
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
              
                ///Son eklenen sorular başta gözükmesi için sıraladık.
                List<Exam> exams = db.Exams.OrderByDescending(x=>x.CreatedDate).ToList();

                return View(exams);
            }
            
        }

        public IActionResult CreateExam()
        {

            List<ArticleViewModel> articles = GetArticleContent();

            return View(articles);
        }

        [HttpPost]
        public bool CreateExam([FromBody]ExamViewModel data)
        {
            ///Önce sınavda başlık ve makaleyi veritabanına kaydediyoruz
            Exam exam = new Exam()
            {
                Title = data.Title,
                Content = data.Content
            };

            using (var db = new Context())
            {
                db.Add(exam);
                db.SaveChanges();
            }

            ///Daha sonra ise sınava ait soruları veritabanına ekliyoruz
            data.QuestionList.ForEach(question => {

                question.ExamId = exam.ExamId;
               
                using (var db = new Context())
                {
                    db.Add(question);
                    db.SaveChanges();
                }

            });

            return true;
        }

        public IActionResult SolveExam(int id)
        {
            ///Sınav bilgilerini gönderiyoruz
            using (var db = new Context())
            {
                ExamViewModel exam = new ExamViewModel()
                {
                    Title = db.Exams.Where(x=>x.ExamId==id).Select(x=>x.Title).SingleOrDefault(),
                    Content = db.Exams.Where(x => x.ExamId == id).Select(x => x.Content).SingleOrDefault(),
                    QuestionList = db.Questions.Where(x => x.ExamId == id).ToList()
                };

                TempData["id"] = id;

                return View(exam);
            }

        }

        [HttpPost]
        public bool[] SolveExam([FromBody]string[] answers)
        {
            int id = (int)TempData["id"];
            using (var db = new Context())
            {
                ///Ön taraftan cevaplanan sorular geliyor
                List<Question> questionList = db.Questions.Where(x => x.ExamId == id).ToList();
                bool[] correctAnswers = new bool[4];

                ///Eğer cevaplanan soru ile doğru cevap eşit ise true döndürüyor
                for(int i=0; i<4;i++)
                correctAnswers[i] = (questionList.ElementAt(i).CorrectAnswer) == answers[i];

                return correctAnswers;
            }
            
        }


        public IActionResult DeleteExam(int id)
        {
            using (var db = new Context())
            {
                ///Makale silme işlemi
                var exam = db.Exams.Where(x => x.ExamId == id).SingleOrDefault();
                var questionList = db.Questions.Where(x => x.ExamId == id).ToList();
                db.Remove(exam);
                db.RemoveRange(questionList);
                db.SaveChanges();

                return RedirectToAction("Index");
            }

        }

        [AllowAnonymous]
        [Route("Account/Login")]
        public IActionResult Login()
        {
            return View();
        }

        [AllowAnonymous]
        [Route("Account/Login")]
        [HttpPost]
        public IActionResult Login(User user)
        {


            Expression<Func<User, bool>> filter = x => x.Username == user.Username && x.Password == user.Password;
            using (var db = new Context())
            {
                User currentUser = db.Users.Where(filter).SingleOrDefault();
                ClaimsIdentity identity = null;
                if (currentUser != null)
                {

                    identity = new ClaimsIdentity(new[] {
                    new Claim(ClaimTypes.Name, currentUser.Username)

                }, CookieAuthenticationDefaults.AuthenticationScheme);

                    ///Oturum açma işlemi
                    var principal = new ClaimsPrincipal(identity);
                    var login = HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    ///Oturum açma işlemi başarısız ise ön tarafa TempData ile hata yolluyoruz
                    TempData["warning"] = "Kullanıcı adı ya da şifre bilgileri hatalı.";
                }
            }

            return View();
        }

        public IActionResult Logout()
        {


            var login = HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }


        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }


        public static async Task<List<ISyndicationContent>> RssFeedReader()
        {
            ///Wired Sitesindeki Rss beslemesinden son haberlerin linklerini ve başlıklarını çekiyoruz
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

            ///Linklere giderek makaleleri çekiyoruz
            List<ArticleViewModel> articleList = new List<ArticleViewModel>();
            var contentList = RssFeedReader().Result;

            contentList.Take(5).ToList().ForEach(content =>
            {
                String articleUrl = content.Fields.Where(y => y.Name == "link").Select(z => z.Value).SingleOrDefault();

                HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
                WebClient client = new WebClient();
                doc.LoadHtml(client.DownloadString(articleUrl));

                ///Makaleler article__body altında sınıflarda bulunuyor. Bu yüzden bu sınıf tanımlı nodeları elde ediyoruz.
                var articleBody = doc.DocumentNode.SelectNodes("//div[contains(@class,'article__body')]");
                if (articleBody!=null) { 
                StringBuilder articleText = new StringBuilder();
                for (int i = 0; i < articleBody.Count - 1; i++)
                {
                    var innerNodes = articleBody[i];

                    foreach (var node in innerNodes.SelectNodes("*"))
                    {
                        ///Çekilen nodelarda istenmeyen yazılar mevcut, bu yüzden p etiketi dışındakileri dahil etmiyoruz
                        if (node.Name == "p")
                        {
                            articleText.Append(node.InnerText);
                        }
                    }


                }

                ///Metnin son halini makale listesine ekliyoruz.
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
