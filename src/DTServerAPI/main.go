package main

import (
	"context"
	"fmt"
	"github.com/labstack/echo/v4"
	"go.mongodb.org/mongo-driver/bson"
	"go.mongodb.org/mongo-driver/mongo"
	"go.mongodb.org/mongo-driver/mongo/options"
	"log"
	"net/http"
	"os"
	"time"
)

type Process struct {
	Id          string `json:"id"`
	Name        string `json:"name"`
	TimeStarted string `json:"timeStarted"`
	IsRunning   bool   `json:"isRunning"`
	TimeEnded   string `json:"timeEnded"`
	Duration    string `json:"duration"`
	Threads     string `json:"threads"`
	MemoryUsage string `json:"memoryusage"`
}

type Doc struct {
	Token     string    `json:"token"`
	Processes []Process `json:"proccesses"`
}

func main() {
	// main context for client creation
	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()

	// client creation using the context created above
	client, err := mongo.Connect(ctx, options.Client().ApplyURI(os.Getenv("MONGO_URI")))
	if err != nil {
		log.Fatal(err)
	}
	defer func() {
		if err = client.Disconnect(context.Background()); err != nil {
			log.Fatal(err)
		}
	}()

	collection := client.Database("db").Collection("processes")

	e := echo.New()

	e.GET("/", func(c echo.Context) error {
		return c.String(http.StatusOK, "Hello, World!")
	})

	e.POST("/process", func(c echo.Context) error {
		// separate context with its own timeout for each request
		reqCtx, reqCancel := context.WithTimeout(context.Background(), 10*time.Second)
		defer reqCancel()

		token := c.Request().Header.Get("token")
		process := Process{
			Id:          c.FormValue("id"),
			Name:        c.FormValue("name"),
			TimeStarted: c.FormValue("timeStarted"),
			IsRunning:   c.FormValue("isRunning") == "true",
			TimeEnded:   c.FormValue("timeEnded"),
			Duration:    c.FormValue("timeDuration"),
			Threads:     c.FormValue("threads"),
			MemoryUsage: c.FormValue("memoryUsage"),
		}

		filter := bson.D{{Key: "token", Value: token}}
		update := bson.D{{Key: "$push", Value: bson.D{{Key: "proccesses", Value: process}}}}

		upsert := true
		after := options.After
		opts := options.FindOneAndUpdateOptions{
			ReturnDocument: &after,
			Upsert:         &upsert,
		}

		err = collection.FindOneAndUpdate(reqCtx, filter, update, &opts).Err()
		if err != nil {
			log.Fatal(err)
			return c.String(http.StatusInternalServerError, "There was a problem with the request.")
		}
		fmt.Println("Request success.")
		return c.String(http.StatusOK, "Request success.")
	})

	e.Logger.Fatal(e.Start(":8080"))
}
