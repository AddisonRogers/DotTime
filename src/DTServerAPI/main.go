package main

import (
	"fmt"
	"log"
	"net/http"
)

func main() {
	serverMux := http.NewServeMux()

	serverMux.HandleFunc("/", func(w http.ResponseWriter, r *http.Request) {
		fmt.Fprintf(w, "Hello, you've requested: %s\n", r.URL.Path)
	})

	fmt.Println("Listening on port 8080...")
	err := http.ListenAndServe(":8080", serverMux)
	if err != nil {
		log.Fatal(err)
	}
}
