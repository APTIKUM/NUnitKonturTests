using Microsoft.Extensions.Configuration;
using OpenQA.Selenium;
using OpenQA.Selenium.BiDi.BrowsingContext;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System.Net.Http.Headers;
using System.Text.Json;

//1.Структура теста — есть Setup и Teardown, авторизация вынесена в отдельный метод
//2. Переиспользование кода — повторяющиеся блоки вынесены в отдельные методы
//3. Нет лишних UI-действий — например, используем переход по URL вместо клика по кнопкам меню, если этого не требуется для проверки в рамках теста
//4. Понятные сообщения в ассертах — при падении теста сразу ясно, что пошло не так
//5. Человекочитаемые названия тестов — проверяющий понимает, что именно тестируется
//6. Уникальные локаторы — используются там, где это возможно
//7. Явные или неявные ожидания — тесты не падают из-за гонки с интерфейсом

namespace NUnitKonturTests
{
    public class Tests
    {
        private IWebDriver _driver;
        private WebDriverWait _waitDriver;
        private IConfiguration _configuration;

        [SetUp]
        public void Setup()
        {
            var builder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", false)
                .AddJsonFile("appsettings.Development.json", true);

            _configuration = builder.Build();

            _driver = new ChromeDriver();
            
            _driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);

            _waitDriver = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
        }

        [TearDown]
        public void Teardown()
        {
            _driver.Quit();
            _driver.Dispose();
        }

        private void Authorize()
        {
            var login = _configuration["Auth:Login"];
            var password = _configuration["Auth:Password"];

            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
            {
                throw new Exception("Нет логина или пароля в конфиге");
            }

            _driver.Navigate().GoToUrl("https://staff-testing.testkontur.ru/");

            var loginInput = _driver.FindElement(By.Id("Username"));
            loginInput.SendKeys(login);

            var passwordInput = _driver.FindElement(By.Id("Password"));
            passwordInput.SendKeys(password);

            var loginBtn = _driver.FindElement(By.Name("button"));
            loginBtn.Click();

            _waitDriver.Until(ExpectedConditions.ElementToBeClickable(
                By.CssSelector("[data-tid='Title']")));
        }

        [Test]
        public void AuthorizationTest()
        {
            Authorize();

            Assert.That(_driver.Title, Does.Contain("Новости"),
                "После авторизации заголовок страницы не содержит 'Новости'");
        }

        [Test]
        public void NavigationMenuTest()
        {
            Authorize();

            var SidebarMenuButton = _driver.FindElement(By.CssSelector("[data-tid='SidebarMenuButton']"));
            SidebarMenuButton.Click();

            _waitDriver.Until(ExpectedConditions.ElementIsVisible(By.CssSelector("[data-tid='SidePage__root']")));

            var communityElement = _driver.FindElements(By.CssSelector("[data-tid='Community']"))
                .First(element => element.Displayed);

            communityElement.Click();

            _waitDriver.Until(ExpectedConditions.UrlToBe("https://staff-testing.testkontur.ru/communities"));
            
            var titlePage = _driver.FindElement(By.CssSelector("[data-tid='Title']"));

            Assert.That(titlePage.Text, Does.Contain("Сообщества"), "При переходе на вкладку 'Сообщества' не смогли найти заголовок 'Сообщества'");
        }
        
        //БАГ
        [Test]
        public void ProfileEditNameTest()
        {
            Authorize();

            _driver.Navigate().GoToUrl("https://staff-testing.testkontur.ru/profile/settings/edit");

            var nameInput = _waitDriver.Until(ExpectedConditions.ElementToBeClickable(
                By.CssSelector("[data-tid='FIO'] input")));

            var newUniqueName = $"User-{Guid.NewGuid()}";

            nameInput.Clear();
            nameInput.SendKeys(newUniqueName);

            var saveBtn = _waitDriver.Until(ExpectedConditions.ElementToBeClickable(
                By.CssSelector("[data-tid='PageHeader'] button")));
            
            saveBtn.Click();

            var nameDiv = _waitDriver.Until(ExpectedConditions.ElementToBeClickable(
                By.CssSelector("[data-tid='EmployeeName']")));

            Assert.That(nameDiv.Text, Is.EqualTo(newUniqueName), "Имя не изменилось");
        }

        [Test]
        public void LogoutTest()
        {
            Authorize();

            var SidebarMenuBtn = _waitDriver.Until(ExpectedConditions.ElementToBeClickable(
                By.CssSelector("[data-tid='SidebarMenuButton']")));
            SidebarMenuBtn.Click();

            var logoutBtn = _waitDriver.Until(ExpectedConditions.ElementToBeClickable(
                By.CssSelector("[data-tid='LogoutButton']")));
            logoutBtn.Click();

            _waitDriver.Until(ExpectedConditions.UrlContains("/Account/Logout"));

            _driver.Navigate().GoToUrl("https://staff-testing.testkontur.ru");

            _waitDriver.Until(ExpectedConditions.UrlContains("/Account/Login"));

            Assert.That(_driver.Url, Does.Contain("/Account/Login"), "После выхода пользователь не перенаправляется на страницу входа");
        }


        [Test]
        public void SearchEmployeeTest()
        {
            Authorize();

            var searchBarContainer = _waitDriver.Until(ExpectedConditions.ElementToBeClickable(
                By.CssSelector("[data-tid='SearchBar']")));
            searchBarContainer.Click();

            var searchInput = _waitDriver.Until(driver =>
                searchBarContainer.FindElement(By.CssSelector("input")));

            var employeeToSearch = "vc615774@gmail.com";
            var emploeeNameToAssert = "Черкасов Владислав Димитриевич";
            // почему бы не искать самого себя? Так мы гарантируем то - что сотрудник существует
            // ну я думаю здесь нужен какой-нибудь тестовый сотрудник для его поиска,
            // но я такого не знаю 😶‍🌫️

            searchInput.Clear();
            searchInput.SendKeys(employeeToSearch);

            var scrollSearchResultsContainer = _waitDriver.Until(ExpectedConditions.ElementToBeClickable(
                By.CssSelector("[data-tid='ScrollContainer__inner']")));

            var searchDivs = scrollSearchResultsContainer.FindElements(
                By.CssSelector("[data-tid='Item'] div[title]"));

            var searchEmployee = searchDivs.FirstOrDefault(div => div.Text.Contains(employeeToSearch));

            //можно и здесь остановится , а можно пойти дальше,
            //как вы и говорили - проверить какие-нибудь данные на странице найденного пользователя

            //Assert.That(searchEmployee, Is.Not.Null, $"Сотрудник '{employeeToSearch}' не найден");

            searchEmployee.Click();

            var userNameDiv = _waitDriver.Until(ExpectedConditions.ElementToBeClickable(
                By.CssSelector("[data-tid='EmployeeName']")));

            //здесь наверное лучше проверять какую-нибудь неизменяемую инфу,
            //например guid в url , но мне уже лень (если возьмете в контур, я не буду лениться💓)
            Assert.That(userNameDiv.Text, Is.EqualTo(emploeeNameToAssert), 
                $"Имя найденного сотрудника - {userNameDiv.Text}. Но ожидалось - {emploeeNameToAssert}");


            //у этого кода есть недостаток - нельзя нормально использовать Assert ,
            //потому что исклбчение ловится перед Assert'ом, 
            //и конечно можно обернуть все в try catch и использовать  Assert.Fail(),
            //но мне кажется решение выше лучше

            //var searchUserElement = _waitDriver.Until(driver =>
            //{
            //    var searchElements = driver.FindElements(By.CssSelector("div.react-ui > *"));

            //    return searchElements.FirstOrDefault(e => e.Text.Contains(userToSearch));
            //});

            //Assert.That(searchUserElement, Is.Not.Null,
            //    $"Элемент '{userToSearch}' не найден");
        }

        /// <summary>
        /// Подготовка перед тестом на лайк под комментарием. Убираем (или ставим) свой лайк, если он там уже есть.
        /// Как и говорили на занятии - надо подготовить почву 
        /// (на занятии говорили про сообщества, типа надо их удалять после тестовых созданий, 
        /// ну вот с лайками также наверное)
        /// </summary>
        /// <param name="isSetLike">поставить/убрать лайк</param>
        private async void PrepareEditLikeCommentTest(bool isSetLike)
        {
            var apiHelper = new ApiStaffTestingHelper();
            await apiHelper.AuthAsync();

            await apiHelper.EditLikeCommentAsync(_configuration["Comment:Guid"], isSetLike);
        }

        //БАГ
        //Я уверен что нарушил все патерны програмирования, но оно работает.
        //Я бы переписал , но уже нет времени. Этот тест сожрал слишком много времени
        [Test]
        public void LikeCommentTest()
        {
            PrepareEditLikeCommentTest(false);

            Authorize();

            _driver.Navigate().GoToUrl("https://staff-testing.testkontur.ru/communities/aa580a76-1d03-4116-83fd-7327dda8eec0?tab=discussions&id=cef5d3cc-9bfd-4d98-b867-f7f96a313964");

            var moreCommentsBtn = _waitDriver.Until(ExpectedConditions.ElementToBeClickable(
                By.CssSelector("[data-tid='CommentsToggle'")));

            moreCommentsBtn.Click();

            var commentsDiv = _waitDriver.Until(ExpectedConditions.ElementToBeClickable(
                By.CssSelector("[data-tid='CommentsList'")));

            var commentItemDiv = commentsDiv.FindElement(
                By.XPath($".//*[@data-tid='TextComment' and contains(text(), " +
                $"'{_configuration["Comment:Guid"]}')]/ancestor::*[@data-tid='CommentItem']"));

            var likeCommentBtn = commentItemDiv.FindElements(By.CssSelector("button"))
                .Last();

            var prevCountLikes = 0;

            
            var likeCountSpan = likeCommentBtn.FindElements(By.CssSelector("span"))
                .FirstOrDefault();
            // span объекта нет , если лайков 0
            if (likeCountSpan != null && int.TryParse(likeCountSpan.Text, out var count))
            {
                prevCountLikes = count;
            }

            likeCommentBtn.Click();

            var currCountLikes = 0;

            likeCountSpan = likeCommentBtn.FindElements(By.CssSelector("span"))
                .FirstOrDefault();

            if (likeCountSpan != null && int.TryParse(likeCountSpan.Text, out count))
            {
                currCountLikes = count;
            }

            Assert.That(prevCountLikes + 1 == currCountLikes, "Количество лайков не изменилось");
            // я бы еще проверил цвет лайка - но что-то не понял ничего. Он и так и так currentColor
        }


    }
}