using System.Net;
using Newtonsoft.Json;

public class Account
{
    public string Name { get; set; }
}

namespace BPMAccountCounter
{
    class Program
    {
        static string _appUrl = "http://somebpmapp.ru"; //Адрес приложения (ссылка на основной сайт)
        static string _authServiceUrl = _appUrl + "/ServiceModel/AuthService.svc/Login"; //Ссылка на сервис авторизации
        static string _userName = "Login"; //Логин
        static string _userPassword = "Password"; //Пароль

        static void Main(string[] args)
        {
            //Данные аутентификации
            var authData = @"{
                ""UserName"":""" + _userName + @""",
                ""UserPassword"":""" + _userPassword + @"""
            }";

            var request = CreateRequest(_authServiceUrl, authData); //Запрос к сервису авторизации
            var _authCookie = new CookieContainer(); //Инициализация куки
            request.CookieContainer = _authCookie; //Подключение куки к запросу

            //Код запроса к серверу авторизации
            using (var response = (HttpWebResponse)request.GetResponse())
            {
                if (response.StatusCode == HttpStatusCode.OK) //Если запрос успешен
                {
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        var responseMessage = reader.ReadToEnd(); //Чтение содержимого запроса
                        if (responseMessage.Contains("\"Code\":1")) //Если авторизация не успешна
                        {
                            throw new UnauthorizedAccessException($"Unauthorized {_userName} for {_appUrl}");
                        }
                    }
                    string authName = ".ASPXAUTH";
                    string authCookieValue = response.Cookies[authName].Value;
                    _authCookie.Add(new Uri(_appUrl), new Cookie(authName, authCookieValue)); //Загрузка полученных данных авторизации в куки
                }
            }

            request = CreateRequest(_appUrl + "/0/odata/Account"); //Запрос к таблице Контрагентов в OData
            request.Method = "GET"; //Метод запроса

            //Подключение заголовков для запроса и полученных куки
            request.Headers.Add("ForceUseSession", "true");
            request.CookieContainer = _authCookie;
            AddCsrfToken(request);

            var cnt = 0; //Переменная для подсчёта количества объектов
            using (var response = (HttpWebResponse)request.GetResponse()) //Запрос к OData
            {
                if (response.StatusCode == HttpStatusCode.OK) //Если запрос успешен
                {
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        var responseMessage = reader.ReadToEnd();
                        dynamic data = JsonConvert.DeserializeObject(responseMessage); //Распаковка полученных данных формата JSON
                        foreach (dynamic item in data.value) //Проходим по полученным записям
                        {
                            string name = item.Name;
                            if (name.Contains('А') || name.Contains('а'))
                            {
                                cnt += 1;
                            }
                        }
                    }
                }
            }
            Console.WriteLine("Подсчитано через OData: {0}", cnt);

            //То же самое, но для запроса к веб-сервису
            request = CreateRequest(_appUrl + "/0/ServiceModel/AccountService.svc/AccountRequest?sub=А");
            request.Method = "GET";
            cnt = 0;
            using (var response = (HttpWebResponse)request.GetResponse())
            {
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        var responseMessage = reader.ReadToEnd();
                        dynamic data = JsonConvert.DeserializeObject(responseMessage);
                        string result = data.AccountRequestResult;
                        cnt = int.Parse(result); //Чтение в формате Integer
                    }
                }
            }
            Console.WriteLine("Подсчитано через Веб-сервис: {0}", cnt);
        }

        //Запрос к сервису аутентификации
        static HttpWebRequest CreateRequest(string url, string requestData = null)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.ContentType = "application/json";
            request.Method = "POST";
            request.KeepAlive = true;
            if (!string.IsNullOrEmpty(requestData))
            {
                using (var requestStream = request.GetRequestStream())
                {
                    using (var writer = new StreamWriter(requestStream))
                    {
                        writer.Write(requestData);
                    }
                }
            }
            return request;
        }

        //Извлечение CSRF-токена для подключения к OData
        static void AddCsrfToken(HttpWebRequest request)
        {
            var cookie = request.CookieContainer.GetCookies(new Uri(_appUrl))["BPMCSRF"];
            if (cookie != null)
            {
                request.Headers.Add("BPMCSRF", cookie.Value);
            }
        }
    }
}