package main

import (
	"github.com/labstack/echo/v4"
	"net/http"
)

type process struct {
	id int
	name string
	time int

}

func main() {
	e := echo.New()
	e.GET("/", func(c echo.Context) error {
		return c.String(http.StatusOK, "Hello, World!")
	})
	e.POST("/users", saveUser)
	e.GET("/users/:id", getUser)
	e.PUT("/users/:id", updateUser)
	e.DELETE("/users/:id", deleteUser)

	// e.GET("/users/:id", getUser)
	func getUser(c echo.Context) error {
		// User ID from path `users/:id`
		id := c.Param("id")
		return c.String(http.StatusOK, id)
	}

	//e.GET("/show", show)
	func show(c echo.Context) error {
		// Get team and member from the query string
		team := c.QueryParam("team")
		member := c.QueryParam("member")
		return c.String(http.StatusOK, "team:" + team + ", member:" + member)
	}

	e.Logger.Fatal(e.Start(":8080"))
}
