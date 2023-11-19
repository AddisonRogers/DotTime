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
	Token     string    `json:"token"`
	Version   string    `json:"version"`
	Processes []ProcessDB `json:"processes"`
}

type ProcessDB struct {
	Name         string           `json:"name"`
	ProcessCount []string         `json:"processCount"`
	History      []ProcessHistory `json:"history"`
}

type Process struct {
	Name         string         `json:"name"`
	ProcessCount string         `json:"processCount"`
	History      ProcessHistory `json:"history"`
}

type Doc struct {
	Token     string    `json:"token"`
	Version   string    `json:"version"`
	Processes []Process `json:"processes"`
}

/*
{
    "token": "xxxxx",
	"version": "1.0.0",
    "processes": [
        {
            "id": "1",
            "name": "test",
            "processCount": ["5", "4"],
            "history": [{
				"timeStarted": "2022-01-01 00:00:00",
				"timeEnded": "2022-01-01 00:00:00"
				},
				{
				"timeStarted": "2022-01-01 00:00:00",
				"timeEnded": "2022-01-01 00:00:00"
				}],
        }
    ]
}
*/

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
				for _, existingProcess := range existingDoc.Processes { // For all the processes that are already in the database
					if process.Name == existingProcess.Name {
						// We found a matching process
						// process.History.TimeStarted
						if (process.History.TimeStarted == existingProcess.History[len(existingProcess.History)-1].TimeStarted) && (process.History.TimeEnded == nil) {
							// We found a matching history
							// Update the existing history
							existingProcess.History[len(existingProcess.History)-1].TimeEnded = process.History.TimeEnded
							goto UPDATE
						}
					}
					// No matching process found, append new process
					//existingProcess.History = append(existingProcess.History, process.History)
				}
				// No process found, append new process to the document
				existingDoc.Processes = append(existingDoc.Processes,
					ProcessDB{
						Name: process.Name,
						ProcessCount: []string{process.ProcessCount},
						History: []ProcessHistory{process.History},
					})
			}

			// No matching history found, append new process
			existingDoc.Processes = append(existingDoc.Processes, doc.Processes...)

		// Using the filter to find the document in the collection
		// then with the document found, if we have a process with the same starting time value but no ending value then we will instead update the value in process.history[n] otherwise if it is a new history value we push the new process to the array
		//

		UPDATE:

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

	e.Logger.Fatal(e.Start(":1323"))
}
