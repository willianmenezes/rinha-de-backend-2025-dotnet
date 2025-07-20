CREATE UNLOGGED TABLE payments (
    correlationId UUID PRIMARY KEY,
    amount DECIMAL NOT NULL,
    requested_at TIMESTAMP NOT NULL,
    fallback BOOLEAN NOT NULL
);

CREATE INDEX payments_requested_at ON payments (requested_at);

CREATE UNLOGGED TABLE healthcheck (
    id UUID PRIMARY KEY,
    best_client CHAR(8) NOT NULL,
    requested_at TIMESTAMP NOT NULL
);