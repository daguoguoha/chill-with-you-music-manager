package main

import (
	"io"
	"log"
)

func init() { log.SetOutput(io.Discard) }
