package main

import (
	"github.com/labstack/echo/v4"
	"log"
	"net/http"
	"strconv"
)

type updateProcess struct {
	id           int
	name         string
	timeStarted  int
	timeEnded    int
	timeDuration int
	threads      int
	memoryUsage  int
}

type process struct {
	id          int
	name        string
	history     []processTime
	threads     []int
	memoryUsage []int
}

type processTime struct {
	timeStarted  int
	timeEnded    int
	timeDuration int
}

func conv(s string) int {
	i, err := strconv.Atoi(s)
	if err != nil {
		log.Default().Println(err)
	}
	return i
}

func main() {
	e := echo.New()
	e.GET("/", func(c echo.Context) error {
		return c.String(http.StatusOK, "Hello, World!")
	})

	e.POST("/process", func(c echo.Context) error {
		token := c.Request().Header.Get("token") // Log the process to the token primary key
		_process := updateProcess{
			id:           conv(c.FormValue("id")),
			name:         c.FormValue("name"),
			timeStarted:  conv(c.FormValue("timeStarted")),
			timeEnded:    conv(c.FormValue("timeEnded")),
			timeDuration: conv(c.FormValue("timeDuration")),
			threads:      conv(c.FormValue("threads")),
			memoryUsage:  conv(c.FormValue("memoryUsage")),
		}

		return c.String(http.StatusOK, "Request success.")
	})

	e.Logger.Fatal(e.Start(":8080"))
}
