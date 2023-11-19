package main

import (
	"context"
	"errors"
	"fmt"
	"github.com/labstack/echo/v4"
	"go.mongodb.org/mongo-driver/bson"
	"go.mongodb.org/mongo-driver/mongo"
	"go.mongodb.org/mongo-driver/mongo/options"
	"log"
	"net/http"
	"time"
)

type ProcessHistory struct {
	TimeStarted string  `json:"timeStarted"`
	TimeEnded   *string `json:"timeEnded,omitempty"`
}

type DocDB struct {
	Token     string      `json:"token"`
	Processes []ProcessDB `json:"processes"`
}

type ProcessDB struct {
	Name    string           `json:"name"`
	History []ProcessHistory `json:"history"`
}

type Process struct {
	Name    string         `json:"name"`
	History ProcessHistory `json:"history"`
}

type Doc struct {
	Token     string    `json:"token"`
	Version   string    `json:"version"`
	Processes []Process `json:"processes"`
}

func main() {
	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()

	client, err := mongo.Connect(ctx, options.Client().ApplyURI("mongodb://localhost:27017"))
	if err != nil {
		log.Fatal(err)
	}
	defer func() {
		if err = client.Disconnect(context.Background()); err != nil {
			log.Fatal(err)
		}
	}()

	collection := client.Database("db").Collection("processes")
	fmt.Println("Connected to MongoDB.")

	e := echo.New()

	e.GET("/", func(c echo.Context) error {
		return c.String(http.StatusOK, "Hello, World!")
	})

	e.POST("/process", func(c echo.Context) error {
		reqCtx, reqCancel := context.WithTimeout(context.Background(), 10*time.Second)
		defer reqCancel()

		doc := new(Doc)

		if err = c.Bind(doc); err != nil {
			return echo.NewHTTPError(http.StatusBadRequest, "Invalid format")
		}

		filter := bson.D{{Key: "token", Value: doc.Token}}

		var existingDoc DocDB
		err = collection.FindOne(reqCtx, filter).Decode(&existingDoc)
		if errors.Is(mongo.ErrNoDocuments, err) {
			// Insert the new document if it doesn't exist
			_, err = collection.InsertOne(reqCtx, doc)
			if err != nil {
				log.Fatal(err)
				return c.String(http.StatusInternalServerError, "There was a problem with request.")
			}
			fmt.Println("Request success.")
			return c.String(http.StatusOK, "Request success.")
		} else if err != nil {
			log.Fatal(err)
			return c.String(http.StatusInternalServerError, "There was a problem with the request.")
		} else {
			// We found an existing document
			for _, process := range doc.Processes { // For all the processes that has been sent in the post request

				matchFound := false
				for i, existingProcess := range existingDoc.Processes {
					if process.Name != existingProcess.Name {
						continue
					}
					// We found a matching process
					if (process.History.TimeStarted == existingProcess.History[len(existingProcess.History)-1].TimeStarted) && (existingProcess.History[len(existingProcess.History)-1].TimeEnded == nil) {
						existingDoc.Processes[i].History[len(existingProcess.History)-1].TimeEnded = process.History.TimeEnded
						matchFound = true
						break
					}
					existingDoc.Processes[i].History = append(existingProcess.History, process.History)
					matchFound = true
				}

				// If no matching process was found, add a new process.
				if !matchFound {
					existingDoc.Processes = append(existingDoc.Processes, ProcessDB{
						Name:    process.Name,
						History: []ProcessHistory{process.History},
					})
				}
			}
		}

		update := bson.D{{Key: "$push", Value: bson.D{{Key: "processes", Value: doc}}}}
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

	e.Logger.Fatal(e.Start(":1323"))

}
