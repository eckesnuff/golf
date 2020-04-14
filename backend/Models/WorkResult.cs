namespace backend.Models
{

    public class Result
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public static Result Error(string message)
        {
            return new Result
            {
                Success = false,
                Message = message
            };
        }
        public static Result OK(string message = null)
        {
            return new Result
            {
                Success = true,
                Message = message
            };
        }
        // public static Result<T> OK<T>(T data, string message = null)
        // {
        //     return new Result<T>
        //     {
        //         Success = true,
        //         Message = message,
        //         Data=data
        //     };
        // }
    }
    public static class ResultExtension
    {
        public static Result<T> WithData<T>(this Result result, T data)
        {
            return new Result<T>(result)
            {
                Data = data
            };

        }
    }
    public class Result<T> : Result
    {
        // public Result(){}
        public Result(Result result)
        {
            this.Message = result.Message;
            this.Success = result.Success;
        }
        public T Data { get; set; }
    }
    public class WorkResult
    {
        public static WorkResult Error(string message, Status status = Status.CommonError)
        {
            return new WorkResult
            {
                StatusCode = status,
                Message = message
            };
        }
        public bool Success { get => StatusCode == Status.OK; }
        public Status StatusCode { get; set; }
        public string Message { get; set; }
        public enum Status
        {
            OK = 0,
            CommonError = 1,
            LoginFailure = 2,
            ConnectionError = 3,
            ParseFailure = 4
        }
    }
    public class WorkResult<T> : WorkResult
    {
        public T ResultData { get; set; }

        public static WorkResult<T> OK(T data)
        {
            return new WorkResult<T>
            {
                StatusCode = Status.OK,
                ResultData = data
            };
        }

    }
}
