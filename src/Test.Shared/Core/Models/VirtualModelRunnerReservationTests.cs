namespace Test.Shared.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using Conductor.Core.Enums;
    using Conductor.Core.Helpers;
    using Conductor.Core.Models;
    using FluentAssertions;

    /// <summary>
    /// Unit tests for VMR reservation models.
    /// </summary>
    public class VirtualModelRunnerReservationTests
    {
        public void Reservation_DefaultsAreUsable()
        {
            VirtualModelRunnerReservation reservation = new VirtualModelRunnerReservation();

            reservation.Id.Should().StartWith(IdGenerator.VirtualModelRunnerReservationPrefix);
            reservation.Name.Should().Be("VMR Reservation");
            reservation.Active.Should().BeTrue();
            reservation.Subjects.Should().BeEmpty();
            reservation.Labels.Should().BeEmpty();
            reservation.Tags.Should().BeEmpty();
            reservation.EndUtc.Should().BeAfter(reservation.StartUtc);
        }

        public void Reservation_NullCollectionsBecomeEmpty()
        {
            VirtualModelRunnerReservation reservation = new VirtualModelRunnerReservation
            {
                Subjects = null,
                Labels = null,
                Tags = null
            };

            reservation.Subjects.Should().NotBeNull().And.BeEmpty();
            reservation.Labels.Should().NotBeNull().And.BeEmpty();
            reservation.Tags.Should().NotBeNull().And.BeEmpty();
        }

        public void Reservation_FromDataRow_MapsFieldsAndJson()
        {
            DateTime start = DateTime.UtcNow.AddHours(1);
            DateTime end = start.AddHours(2);
            DataTable table = CreateReservationTable();
            table.Rows.Add(
                "vmrr_test",
                "ten_test",
                "vmr_test",
                "Maintenance Window",
                "Dedicated benchmark run",
                start,
                end,
                60000,
                true,
                "usr_creator",
                "cred_creator",
                "[\"benchmark\"]",
                "{\"purpose\":\"load-test\"}",
                "{\"ticket\":\"INC-123\"}",
                start.AddDays(-1),
                start.AddDays(-1));

            VirtualModelRunnerReservation reservation = VirtualModelRunnerReservation.FromDataRow(table.Rows[0]);

            reservation.Id.Should().Be("vmrr_test");
            reservation.TenantId.Should().Be("ten_test");
            reservation.VirtualModelRunnerId.Should().Be("vmr_test");
            reservation.Name.Should().Be("Maintenance Window");
            reservation.Description.Should().Be("Dedicated benchmark run");
            reservation.StartUtc.Should().Be(start);
            reservation.EndUtc.Should().Be(end);
            reservation.AdmissionDrainLeadMs.Should().Be(60000);
            reservation.Labels.Should().ContainSingle().Which.Should().Be("benchmark");
            reservation.Tags.Should().ContainKey("purpose").WhoseValue.Should().Be("load-test");
            reservation.Metadata.Should().NotBeNull();
        }

        public void Subject_FromDataRow_MapsFields()
        {
            DateTime created = DateTime.UtcNow;
            DataTable table = new DataTable();
            table.Columns.Add("id", typeof(string));
            table.Columns.Add("tenantid", typeof(string));
            table.Columns.Add("reservationid", typeof(string));
            table.Columns.Add("subjecttype", typeof(int));
            table.Columns.Add("subjectid", typeof(string));
            table.Columns.Add("createdutc", typeof(DateTime));
            table.Rows.Add("vmrrs_test", "ten_test", "vmrr_test", (int)ReservationSubjectTypeEnum.User, "usr_test", created);

            VirtualModelRunnerReservationSubject subject = VirtualModelRunnerReservationSubject.FromDataRow(table.Rows[0]);

            subject.Id.Should().Be("vmrrs_test");
            subject.TenantId.Should().Be("ten_test");
            subject.ReservationId.Should().Be("vmrr_test");
            subject.SubjectType.Should().Be(ReservationSubjectTypeEnum.User);
            subject.SubjectId.Should().Be("usr_test");
            subject.CreatedUtc.Should().Be(created);
        }

        public void ReservationEnums_HaveStableValues()
        {
            ((int)ReservationSubjectTypeEnum.Unknown).Should().Be(0);
            ((int)ReservationSubjectTypeEnum.User).Should().Be(1);
            ((int)ReservationSubjectTypeEnum.Credential).Should().Be(2);

            ((int)ReservationDecisionEnum.NotEvaluated).Should().Be(0);
            ((int)ReservationDecisionEnum.NoReservation).Should().Be(1);
            ((int)ReservationDecisionEnum.Allowed).Should().Be(2);
            ((int)ReservationDecisionEnum.Denied).Should().Be(3);
            ((int)ReservationDecisionEnum.AuthenticationRequired).Should().Be(4);
            ((int)ReservationDecisionEnum.Conflict).Should().Be(5);
        }

        private static DataTable CreateReservationTable()
        {
            DataTable table = new DataTable();
            table.Columns.Add("id", typeof(string));
            table.Columns.Add("tenantid", typeof(string));
            table.Columns.Add("vmrid", typeof(string));
            table.Columns.Add("name", typeof(string));
            table.Columns.Add("description", typeof(string));
            table.Columns.Add("startutc", typeof(DateTime));
            table.Columns.Add("endutc", typeof(DateTime));
            table.Columns.Add("admissiondrainleadms", typeof(int));
            table.Columns.Add("active", typeof(bool));
            table.Columns.Add("createdbyuserid", typeof(string));
            table.Columns.Add("createdbycredentialid", typeof(string));
            table.Columns.Add("labels", typeof(string));
            table.Columns.Add("tags", typeof(string));
            table.Columns.Add("metadata", typeof(string));
            table.Columns.Add("createdutc", typeof(DateTime));
            table.Columns.Add("lastupdateutc", typeof(DateTime));
            return table;
        }
    }
}
