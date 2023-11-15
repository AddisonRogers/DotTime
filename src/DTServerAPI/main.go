package main

import (
	"context"
	"fmt"
	"go.mongodb.org/mongo-driver/bson"
	"go.mongodb.org/mongo-driver/mongo"
	"go.mongodb.org/mongo-driver/mongo/options"
	"log"
	"net/http"
	"os"
	"time"

	"github.com/labstack/echo/v4"
)

type ProcessHistory struct {
	TimeStarted string `json:"timeStarted"`
	IsRunning   bool   `json:"isRunning"`
	TimeEnded   string `json:"timeEnded"`
}

type Process struct {
	Id           string           `json:"id"`
	Name         string           `json:"name"`
	ProcessCount []string         `json:"processCount"`
	History      []ProcessHistory `json:"history"`
	Threads      []string         `json:"threads"`
	MemoryUsage  []string         `json:"memoryUsage"`
}

type Doc struct {
	Token     string    `json:"token"`
	Processes []Process `json:"processes"`
}

func main() {
	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()

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

	e.POST("/process", func(c echo.Context) error {
		reqCtx, reqCancel := context.WithTimeout(context.Background(), 10*time.Second)
		defer reqCancel()

		token := c.Request().Header.Get("token")
		doc := new(Doc)

		if err = c.Bind(doc); err != nil {
			return echo.NewHTTPError(http.StatusBadRequest, "Invalid format")
		}

		filter := bson.D{{Key: "token", Value: token}}

		// Loop through each process and each history in the process to update timeEnded where relevant.
		for i := range doc.Processes {
			for j := range doc.Processes[i].History {
				if doc.Processes[i].History[j].IsRunning {
					doc.Processes[i].History[j].TimeEnded = time.Now().String()
				}
			}
		}

		update := bson.D{{Key: "$push", Value: bson.D{{Key: "proccesses", Value: doc}}}}

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
}
