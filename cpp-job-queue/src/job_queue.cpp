#include "job_queue.hpp"

#include <algorithm>
#include <cctype>
#include <stdexcept>
#include <unordered_set>
#include <vector>

namespace benchmark {

namespace {

bool valid_id(const std::string& id) {
    if (id.empty() || id.size() > 64) return false;
    for (unsigned char c : id) {
        if (!(std::isalnum(c) || c == '_' || c == '-')) return false;
    }
    return true;
}

bool valid_payload(const std::string& payload) {
    if (payload.empty() || payload.size() > 256) return false;
    return payload.find('\0') == std::string::npos;
}

} // namespace

struct JobQueue::Impl {
    std::size_t capacity;
    std::uint32_t max_attempts;
    std::vector<Job> jobs; // submission order
    std::unordered_set<std::string> ids;

    std::size_t active() const {
        std::size_t n = 0;
        for (const Job& j : jobs)
            if (j.status == Status::Queued || j.status == Status::Running) ++n;
        return n;
    }
};

JobQueue::JobQueue(std::size_t capacity, std::uint32_t max_attempts)
    : impl_(new Impl) {
    if (capacity == 0 || max_attempts == 0)
        throw std::invalid_argument("capacity and max_attempts must be > 0");
    impl_->capacity = capacity;
    impl_->max_attempts = max_attempts;
}

JobQueue::~JobQueue() { delete impl_; }

bool JobQueue::submit(std::string id, std::string payload, int priority) {
    if (!valid_id(id) || !valid_payload(payload) || priority < -100 || priority > 100)
        return false;
    if (impl_->ids.count(id)) return false;
    if (impl_->active() >= impl_->capacity) return false;
    Job j;
    j.id = std::move(id);
    j.payload = std::move(payload);
    j.priority = priority;
    j.attempts = 0;
    j.status = Status::Queued;
    impl_->ids.insert(j.id);
    impl_->jobs.push_back(std::move(j));
    return true;
}

std::optional<Job> JobQueue::start_next() {
    std::size_t best = impl_->jobs.size();
    for (std::size_t i = 0; i < impl_->jobs.size(); ++i) {
        const Job& j = impl_->jobs[i];
        if (j.status != Status::Queued) continue;
        if (best == impl_->jobs.size() || j.priority > impl_->jobs[best].priority)
            best = i;
    }
    if (best == impl_->jobs.size()) return std::nullopt;
    Job& j = impl_->jobs[best];
    j.status = Status::Running;
    ++j.attempts;
    return j;
}

bool JobQueue::complete(const std::string& id, bool success) {
    for (Job& j : impl_->jobs) {
        if (j.id != id) continue;
        if (j.status != Status::Running) return false;
        if (success) {
            j.status = Status::Succeeded;
        } else if (j.attempts < impl_->max_attempts) {
            j.status = Status::Queued;
        } else {
            j.status = Status::Failed;
        }
        return true;
    }
    return false;
}

bool JobQueue::cancel(const std::string& id) {
    for (Job& j : impl_->jobs) {
        if (j.id != id) continue;
        if (j.status != Status::Queued) return false;
        j.status = Status::Cancelled;
        return true;
    }
    return false;
}

std::optional<Job> JobQueue::find(const std::string& id) const {
    for (const Job& j : impl_->jobs)
        if (j.id == id) return j;
    return std::nullopt;
}

std::vector<Job> JobQueue::list(std::optional<Status> status) const {
    std::vector<Job> out;
    for (const Job& j : impl_->jobs)
        if (!status || j.status == *status) out.push_back(j);
    std::sort(out.begin(), out.end(),
              [](const Job& a, const Job& b) { return a.id < b.id; });
    return out;
}

std::size_t JobQueue::active_count() const { return impl_->active(); }

} // namespace benchmark
