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

namespace ExamCreator.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            using (var db = new ExamCreatorContext())
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

            return View();
        }

        public IActionResult CreateExam()
        {
            var contentList = RssFeedReader("https://www.wired.com/feed/rss").Result;

            return View();
        }

        public IActionResult SolveExam()
        {
            return View();
        }

        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(User user)
        {


            Expression<Func<User, bool>> filter = x => x.Username == user.Username && x.Password == user.Password;
            using (var db = new ExamCreatorContext())
            {
                User currentUser = db.Users.Where(filter).SingleOrDefault();
                if (currentUser != null)
                {

                    return RedirectToAction("Index", "Home");
                }
            }

            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }


        public static async Task<List<ISyndicationContent>> RssFeedReader(string feedUri)
        {
            List<ISyndicationContent> contentList = new List<ISyndicationContent>();
            using (var xmlReader = XmlReader.Create(feedUri, new XmlReaderSettings() { Async = true }))
            {
                var feedReader = new RssFeedReader(xmlReader);

                while (await feedReader.Read())
                {

                    ISyndicationContent content = await feedReader.ReadContent();

                    contentList.Add(content);
                }
            }

            return contentList;
        }

    }
}
